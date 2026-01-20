# Setup DynamoDB to SNS Integration

## 1. Enable DynamoDB Streams
```bash
aws dynamodb update-table \
  --table-name AIToadyQueries \
  --stream-specification StreamEnabled=true,StreamViewType=NEW_AND_OLD_IMAGES
```

## 2. Create SNS Topic
```bash
aws sns create-topic --name AIToadyQueryCreated
# Note the TopicArn from output
```

## 3. Deploy Lambda Function
```bash
cd lambda
npm install
zip -r function.zip .
aws lambda create-function \
  --function-name DynamoDBStreamToSNS \
  --runtime nodejs20.x \
  --role arn:aws:iam::YOUR_ACCOUNT:role/lambda-dynamodb-sns-role \
  --handler dynamodb-stream-to-sns.handler \
  --zip-file fileb://function.zip \
  --environment Variables={SNS_TOPIC_ARN=arn:aws:sns:REGION:ACCOUNT:AIToadyQueryCreated}
```

## 4. Create IAM Role (if needed)
The Lambda needs permissions for:
- DynamoDB Streams (read)
- SNS (publish)
- CloudWatch Logs (write)

## 5. Add DynamoDB Stream Trigger
```bash
aws lambda create-event-source-mapping \
  --function-name DynamoDBStreamToSNS \
  --event-source-arn arn:aws:dynamodb:REGION:ACCOUNT:table/AIToadyQueries/stream/STREAM_ID \
  --starting-position LATEST
```

## 6. Subscribe Orchestrator to SNS
```bash
aws sns subscribe \
  --topic-arn arn:aws:sns:REGION:ACCOUNT:AIToadyQueryCreated \
  --protocol http \
  --notification-endpoint http://YOUR_NGROK_URL/sns
```
