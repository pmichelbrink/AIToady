# AIToady.Orchestrator

Python service that receives AWS SNS messages via HTTP endpoint, processes them with Ollama, and writes results to Redis.

## Setup

1. Install dependencies:
```bash
pip install -r requirements.txt
```

2. Ensure Ollama is running locally:
```bash
ollama serve
```

3. Run the orchestrator:
```bash
python orchestrator.py
```

4. Open browser to http://localhost:5000

5. Enter configuration in the web UI:
   - Redis host and port
   - Ollama model name
   - (Optional) SNS topic ARN

6. Expose endpoint for AWS SNS (for local development):
```bash
ngrok http 5000
```

7. Subscribe SNS topic to your endpoint:
```bash
aws sns subscribe --topic-arn <your-topic-arn> --protocol http --notification-endpoint <your-ngrok-url>/sns
```

## Architecture

- Flask HTTP endpoint receives SNS notifications (no polling)
- Processes messages with Ollama models
- Writes results to AWS ElastiCache (Redis)

## Message Format

SNS messages should contain:
```json
{
  "key": "unique-identifier",
  "prompt": "Your prompt for the model"
}
```
