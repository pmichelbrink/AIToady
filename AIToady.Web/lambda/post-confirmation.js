import { DynamoDBClient } from '@aws-sdk/client-dynamodb';
import { DynamoDBDocumentClient, PutCommand } from '@aws-sdk/lib-dynamodb';

const client = new DynamoDBClient({ region: process.env.AWS_REGION });
const docClient = DynamoDBDocumentClient.from(client);

export const handler = async (event) => {
    console.log('Cognito Post-confirmation trigger event:', JSON.stringify(event, null, 2));
    
    const { userName, request } = event;
    const email = request.userAttributes.email;
    
    try {
        const now = new Date().toISOString();
        
        await docClient.send(new PutCommand({
            TableName: process.env.USERS_TABLE_NAME || 'AIToadyUsers',
            Item: {
                userId: userName,
                email: email,
                queriesRemaining: 10,
                createdAt: now,
                updatedAt: now
            },
            ConditionExpression: 'attribute_not_exists(userId)'
        }));
        
        console.log(`User record created for ${userName}`);
        
    } catch (error) {
        console.error('Error creating user record:', error);
        // Don't throw error to avoid blocking user confirmation
    }
    
    return event;
};