import json
import os
import ollama
import pickle
import numpy as np
from flask import Flask, render_template, jsonify, request
from threading import Thread, Lock

app = Flask(__name__)

# Global state with lock
status_lock = Lock()
training_status = {
    'is_training': False,
    'current_file': '',
    'processed': 0,
    'total': 0,
    'progress': 0
}

class RAGTrainer:
    def __init__(self, db_dir="C:\\Vector DBs"):
        self.db_dir = db_dir
        os.makedirs(db_dir, exist_ok=True)
        self.embeddings = []
        self.documents = []
        self.metadata = []
    
    def count_json_files(self, folders):
        """Count total JSON files in all folders"""
        total = 0
        for folder in folders:
            if os.path.exists(folder):
                for root, _, files in os.walk(folder):
                    total += sum(1 for f in files if f.endswith('.json'))
        return total
    
    def chunk_text(self, text, max_length=2000):
        """Split text into chunks that fit within context window"""
        if len(text) <= max_length:
            return [text]
        
        chunks = []
        for i in range(0, len(text), max_length):
            chunks.append(text[i:i + max_length])
        return chunks
    
    def build_knowledge_base(self, model_name, folders, base_model, embedding_model='nomic-embed-text'):
        """Build vector database from JSON files in multiple folders"""
        global training_status, status_lock
        
        print(f"Starting training with folders: {folders}")
        
        with status_lock:
            training_status['total'] = self.count_json_files(folders)
            training_status['processed'] = 0
            training_status['is_training'] = True
        
        print(f"Total files to process: {training_status['total']}")
        
        self.embeddings = []
        self.documents = []
        self.metadata = []
        
        # Use specified embedding model (pull if needed)
        try:
            ollama.embeddings(model=embedding_model, prompt='test')
        except:
            print(f"Pulling {embedding_model} model...")
            ollama.pull(embedding_model)
        
        for folder in folders:
            print(f"Processing folder: {folder}")
            if not os.path.exists(folder):
                print(f"Folder does not exist: {folder}")
                continue
                continue
                
            for root, _, files in os.walk(folder):
                for filename in files:
                    if filename.endswith('.json'):
                        filepath = os.path.join(root, filename)
                        
                        with status_lock:
                            training_status['current_file'] = filepath
                        
                        if training_status['processed'] % 10 == 0:
                            print(f"Processing file {training_status['processed']}/{training_status['total']}: {filename}")
                        
                        try:
                            with open(filepath, 'r', encoding='utf-8') as f:
                                data = json.load(f)
                                
                                # Extract ThreadName and messages
                                thread_name = data.get('ThreadName', '')
                                messages = data.get('Messages', [])
                                
                                # Combine thread name with all message content
                                text_parts = []
                                if thread_name:
                                    text_parts.append(f"Thread: {thread_name}")
                                
                                for msg in messages:
                                    message_text = msg.get('message', '')
                                    if message_text:
                                        text_parts.append(message_text)
                                
                                text = '\n'.join(text_parts)
                                
                                if not text:
                                    with status_lock:
                                        training_status['processed'] += 1
                                    continue
                                
                                # Chunk text if too large
                                chunks = self.chunk_text(text)
                                
                                for chunk_idx, chunk in enumerate(chunks):
                                    # Generate embedding using dedicated embedding model
                                    embedding = ollama.embeddings(model=embedding_model, prompt=chunk)
                                    
                                    self.embeddings.append(embedding['embedding'])
                                    self.documents.append(chunk)
                                    self.metadata.append({
                                        "filename": filename, 
                                        "path": filepath,
                                        "thread_name": thread_name,
                                        "chunk": chunk_idx if len(chunks) > 1 else None
                                    })
                        except Exception as e:
                            print(f"Error processing {filepath}: {e}")
                        
                        with status_lock:
                            training_status['processed'] += 1
                            training_status['progress'] = int((training_status['processed'] / training_status['total']) * 100)
        
        # Save to disk
        db_path = os.path.join(self.db_dir, f"{model_name}.pkl")
        with open(db_path, 'wb') as f:
            pickle.dump({
                'embeddings': self.embeddings,
                'documents': self.documents,
                'metadata': self.metadata,
                'base_model': base_model
            }, f)
        
        print(f"Vector database saved to: {db_path}")
        
        with status_lock:
            training_status['is_training'] = False
            training_status['current_file'] = 'Complete'
        return len(self.documents)

trainer = RAGTrainer()

@app.route('/')
def index():
    return render_template('trainer.html')

@app.route('/models')
def get_models():
    """Get available Ollama models"""
    try:
        response = ollama.list()
        
        # Handle Model objects with .model attribute
        if hasattr(response, 'models'):
            model_names = [m.model for m in response.models]
        else:
            model_names = []
        
        print(f"Parsed models: {model_names}")
        return jsonify({'models': model_names})
    except Exception as e:
        print(f"Error loading models: {e}")
        import traceback
        traceback.print_exc()
        return jsonify({'models': [], 'error': str(e)})

@app.route('/start_training', methods=['POST'])
def start_training():
    data = request.json
    db_location = data.get('db_location', 'C:\\Vector DBs')
    model_name = data.get('model_name', '')
    folders = data.get('folders', [])
    base_model = data.get('base_model')
    embedding_model = data.get('embedding_model', 'nomic-embed-text')
    
    print(f"Received training request: db_location={db_location}, model_name={model_name}, folders={folders}, model={base_model}, embedding={embedding_model}")
    
    if not model_name or not folders or not base_model:
        return jsonify({'success': False, 'error': 'Missing model name, folders, or base model'})
    
    # Update trainer db directory
    trainer.db_dir = db_location
    os.makedirs(db_location, exist_ok=True)
    
    def train():
        print("Training thread started")
        trainer.build_knowledge_base(model_name, folders, base_model, embedding_model)
        print("Training thread completed")
    
    Thread(target=train, daemon=True).start()
    return jsonify({'success': True})

@app.route('/status')
def get_status():
    with status_lock:
        status_copy = training_status.copy()
    print(f"Status requested: {status_copy}")
    return jsonify(status_copy)

if __name__ == '__main__':
    print("Starting AIToady Trainer...")
    print("Open http://localhost:5001 in your browser")
    app.run(host='0.0.0.0', port=5001, debug=False)