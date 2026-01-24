import pickle
import numpy as np
import ollama
from flask import Flask, render_template, jsonify, request
import os

app = Flask(__name__)

class RAGQuery:
    def __init__(self, db_dir="C:\\Vector DBs"):
        self.db_dir = db_dir
        self.current_db = None
        self.embeddings = []
        self.documents = []
        self.metadata = []
        self.base_model = None
    
    def list_models(self):
        """List available trained models"""
        if not os.path.exists(self.db_dir):
            return []
        return [f.replace('.pkl', '') for f in os.listdir(self.db_dir) if f.endswith('.pkl')]
    
    def load_model(self, model_name):
        """Load a trained vector database"""
        db_path = os.path.join(self.db_dir, f"{model_name}.pkl")
        with open(db_path, 'rb') as f:
            data = pickle.load(f)
            self.embeddings = np.array(data['embeddings'])
            self.documents = data['documents']
            self.metadata = data['metadata']
            self.base_model = data['base_model']
            self.current_db = model_name
        print(f"Loaded {len(self.documents)} documents from {model_name}")
    
    def find_similar(self, query, top_k=3):
        """Find most similar documents to query"""
        # Generate embedding for query
        query_embedding = ollama.embeddings(model='nomic-embed-text', prompt=query)
        query_vec = np.array(query_embedding['embedding'])
        
        # Calculate cosine similarity
        similarities = np.dot(self.embeddings, query_vec) / (
            np.linalg.norm(self.embeddings, axis=1) * np.linalg.norm(query_vec)
        )
        
        # Get top k results
        top_indices = np.argsort(similarities)[-top_k:][::-1]
        return [(self.documents[i], self.metadata[i], similarities[i]) for i in top_indices]
    
    def query(self, question, use_rag=True, default_model='ministral-3:8b'):
        """Query the system with or without RAG"""
        if use_rag and not self.current_db:
            return "No model loaded for RAG query"
        
        # Determine which model to use
        model = self.base_model if self.base_model else default_model
        
        print(f"Query: use_rag={use_rag}, model={model}, current_db={self.current_db}")
        
        if use_rag:
            # Find relevant documents
            results = self.find_similar(question, top_k=3)
            
            # Build context from results
            context = "\n\n".join([doc for doc, _, _ in results])
            
            print(f"RAG Context length: {len(context)} chars")
            
            # Create prompt with context
            prompt = f"""Based on the following information, answer the question.

Context:
{context}

Question: {question}

Answer:"""
        else:
            # Direct query without RAG
            print("Direct query without RAG context")
            prompt = question
        
        # Generate response
        response = ollama.generate(model=model, prompt=prompt)
        return response['response']

rag = RAGQuery()

@app.route('/')
def index():
    return render_template('query.html')

@app.route('/set_db_location', methods=['POST'])
def set_db_location():
    db_location = request.json.get('db_location', 'C:\\Vector DBs')
    rag.db_dir = db_location
    return jsonify({'success': True})

@app.route('/list_models')
def list_models():
    return jsonify({'models': rag.list_models()})

@app.route('/load_model', methods=['POST'])
def load_model():
    model_name = request.json.get('model_name')
    try:
        rag.load_model(model_name)
        return jsonify({'success': True})
    except Exception as e:
        return jsonify({'success': False, 'error': str(e)})

@app.route('/query', methods=['POST'])
def query():
    question = request.json.get('question')
    use_rag = request.json.get('use_rag', True)
    try:
        answer = rag.query(question, use_rag)
        return jsonify({'answer': answer})
    except Exception as e:
        return jsonify({'error': str(e)})

if __name__ == '__main__':
    print("Starting AIToady Query Interface...")
    print("Open http://localhost:5002 in your browser")
    app.run(host='0.0.0.0', port=5002, debug=False)