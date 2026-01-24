# AIToady.Trainer

RAG (Retrieval-Augmented Generation) trainer for building vector databases from large JSON datasets (100,000+ files).

## Setup

1. Install dependencies:
```bash
pip install -r requirements.txt
```

2. Ensure Ollama is running:
```bash
ollama serve
```

3. Run the trainer:
```bash
python trainer.py
```

4. Open browser to http://localhost:5001

## Usage

1. Add one or more folders containing JSON files
2. Select base Ollama model from dropdown
3. Click "Start Training"
4. Monitor progress in real-time

## Features

- Multiple folder support
- Real-time progress tracking
- Shows current file being processed
- Progress bar with percentage
- Builds ChromaDB vector database for RAG queries