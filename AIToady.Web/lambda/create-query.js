const { DynamoDBClient } = require('@aws-sdk/client-dynamodb');
const { DynamoDBDocumentClient, PutCommand, GetCommand, UpdateCommand } = require('@aws-sdk/lib-dynamodb');
const jwt = require('jsonwebtoken');
const jwksClient = require('jwks-rsa');

const client = new DynamoDBClient({ region: process.env.AWS_REGION });
const docClient = DynamoDBDocumentClient.from(client);

const jwks = jwksClient({
    jwksUri: `https://cognito-idp.${process.env.AWS_REGION}.amazonaws.com/${process.env.USER_POOL_ID}/.well-known/jwks.json`
});

const verifyToken = async (token) => {
    try {
        const decoded = jwt.decode(token, { complete: true });
        const kid = decoded.header.kid;
        const key = await jwks.getSigningKey(kid);
        const signingKey = key.getPublicKey();
        
        return jwt.verify(token, signingKey, { algorithms: ['RS256'] });
    } catch (error) {
        throw new Error('Invalid token');
    }
};

exports.handler = async (event) => {
    const headers = {
        'Access-Control-Allow-Origin': '*',
        'Access-Control-Allow-Headers': 'Content-Type,Authorization',
        'Access-Control-Allow-Methods': 'POST,OPTIONS'
    };

    if (event.httpMethod === 'OPTIONS') {
        return { statusCode: 200, headers, body: '' };
    }

    try {
        const authHeader = event.headers?.Authorization || event.headers?.authorization;
        
        if (!authHeader || !authHeader.startsWith('Bearer ')) {
            return {
                statusCode: 401,
                headers,
                body: JSON.stringify({ error: 'Missing or invalid authorization header' })
            };
        }

        const token = authHeader.substring(7);
        const decoded = await verifyToken(token);
        
        const userId = decoded['cognito:username'] || decoded.username;
        const userEmail = decoded.email;
        
        const body = JSON.parse(event.body);
        const { queryText } = body;
        
        if (!queryText) {
            return {
                statusCode: 400,
                headers,
                body: JSON.stringify({ error: 'queryText is required' })
            };
        }

        // Check user's queries remaining
        const userResult = await docClient.send(new GetCommand({
            TableName: process.env.USERS_TABLE_NAME || 'AIToadyUsers',
            Key: { userId }
        }));

        if (!userResult.Item) {
            return {
                statusCode: 404,
                headers,
                body: JSON.stringify({ error: 'User not found' })
            };
        }

        if (userResult.Item.queriesRemaining <= 0) {
            return {
                statusCode: 403,
                headers,
                body: JSON.stringify({ error: 'No queries remaining' })
            };
        }

        // Decrement queries remaining
        await docClient.send(new UpdateCommand({
            TableName: process.env.USERS_TABLE_NAME || 'AIToadyUsers',
            Key: { userId },
            UpdateExpression: 'SET queriesRemaining = queriesRemaining - :dec, updatedAt = :now',
            ExpressionAttributeValues: {
                ':dec': 1,
                ':now': new Date().toISOString()
            }
        }));

        const queryId = `${userId}_${Date.now()}`;
        const timestamp = new Date().toISOString();

        await docClient.send(new PutCommand({
            TableName: process.env.QUERIES_TABLE_NAME || 'AIToadyQueries',
            Item: {
                QueryId: queryId,
                UserId: userId,
                UserEmail: userEmail,
                Timestamp: timestamp,
                QueryText: queryText,
                Status: 0,
                Topic: 0
            }
        }));

        return {
            statusCode: 200,
            headers,
            body: JSON.stringify({ 
                success: true,
                queryId
            })
        };
    } catch (error) {
        console.error('Error:', error);
        console.error('Error details:', JSON.stringify(error, null, 2));
        return {
            statusCode: 500,
            headers,
            body: JSON.stringify({ success: false, error: error.message || 'Internal server error' })
        };
    }
};
