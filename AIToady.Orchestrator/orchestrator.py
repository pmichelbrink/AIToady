import json
import redis
import ollama
from datetime import datetime
from flask import Flask, request, render_template, jsonify
import os

app = Flask(__name__)
CONFIG_FILE = 'config.json'

# Global state
config = {}
logs = []
r = None

def load_config():
    if os.path.exists(CONFIG_FILE):
        with open(CONFIG_FILE, 'r') as f:
            return json.load(f)
    return {}

def save_config(cfg):
    with open(CONFIG_FILE, 'w') as f:
        json.dump(cfg, f)

def add_log(message):
    logs.append({
        'time': datetime.now().strftime('%H:%M:%S'),
        'message': message
    })
    if len(logs) > 100:
        logs.pop(0)

def process_message(message):
    """Process SNS message: generate Ollama response and write to Redis"""
    key = message.get('key')
    
    # Extract QueryText from DynamoDB format
    new_image = message.get('newImage', {})
    query_text = new_image.get('QueryText', {}).get('S')
    
    if not key or not query_text:
        add_log(f"⚠️  Skipping - missing key or QueryText in message")
        return
    
    add_log(f"Processing key: {key}")
    add_log(f"Query: {query_text}")
    
"""     response = ollama.generate(model=config.get('ollama_model', 'llama2'), prompt=query_text)
    
    if r:
        r.set(key, json.dumps({
            'input': query_text,
            'output': response['response']
        }))
        add_log(f"✓ Completed and saved to Redis: {key}")
    else:
        add_log(f"✓ Completed (Redis disabled): {key}") """

@app.route('/')
def index():
    return render_template('index.html')

@app.route('/config')
def get_config():
    return jsonify(load_config())

@app.route('/start', methods=['POST'])
def start():
    global config, r
    
    try:
        config = request.json
        save_config(config)
        
        # Redis is optional
        if config.get('redis_host'):
            r = redis.Redis(
                host=config['redis_host'],
                port=int(config.get('redis_port', 6379)),
                decode_responses=True
            )
            r.ping()
            add_log(f"Connected to Redis: {config['redis_host']}")
        else:
            add_log("Redis disabled - messages will be logged only")
        
        add_log(f"Using Ollama model: {config.get('ollama_model', 'llama2')}")
        
        return jsonify({
            'success': True,
            'endpoint': f'http://localhost:5000/sns',
            'sns_topic': config.get('sns_topic', '')
        })
    except Exception as e:
        return jsonify({'success': False, 'error': str(e)})

@app.route('/logs')
def get_logs():
    return jsonify(logs)

@app.route('/sns', methods=['POST'])
def sns_endpoint():
    data = json.loads(request.data)
    
    if data.get('Type') == 'SubscriptionConfirmation':
        add_log(f"📩 SNS Subscription Confirmation")
        add_log(f"   SubscribeURL: {data.get('SubscribeURL', 'N/A')}")
        return '', 200
    
    if data.get('Type') == 'Notification':
        add_log(f"📨 Received SNS Notification")
        add_log(f"   Subject: {data.get('Subject', 'N/A')}")
        add_log(f"   MessageId: {data.get('MessageId', 'N/A')}")
        
        message = json.loads(data['Message'])
        add_log(f"   Message: {json.dumps(message, indent=2)}")
        
        process_message(message)
        return '', 200
    
    return '', 400

if __name__ == '__main__':
    import logging
    log = logging.getLogger('werkzeug')
    log.setLevel(logging.ERROR)
    
    print("Starting AIToady Orchestrator...")
    print("Open http://localhost:5000 in your browser")
    app.run(host='0.0.0.0', port=5000)
