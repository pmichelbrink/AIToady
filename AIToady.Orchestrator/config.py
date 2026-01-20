import os
from dotenv import load_dotenv

load_dotenv()

REDIS_HOST = os.getenv('REDIS_HOST')
REDIS_PORT = int(os.getenv('REDIS_PORT', 6379))
OLLAMA_MODEL = os.getenv('OLLAMA_MODEL', 'llama2')
