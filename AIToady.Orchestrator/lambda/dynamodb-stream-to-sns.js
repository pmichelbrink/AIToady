const { SNSClient, PublishCommand } = require('@aws-sdk/client-sns');

const sns = new SNSClient({ region: process.env.AWS_REGION });

exports.handler = async (event) => {
    for (const record of event.Records) {
        if (record.eventName === 'INSERT') {
            const newItem = record.dynamodb.NewImage;
            const oldItem = record.dynamodb.OldImage;
            
            await sns.send(new PublishCommand({
                TopicArn: process.env.SNS_TOPIC_ARN,
                Message: JSON.stringify({
                    key: newItem.QueryId.S,
                    prompt: newItem.Query?.S || newItem.Prompt?.S,
                    newImage: newItem,
                    oldImage: oldItem
                })
            }));
        }
    }
};
