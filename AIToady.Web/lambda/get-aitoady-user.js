const { DynamoDBClient } = require('@aws-sdk/client-dynamodb');
const { DynamoDBDocumentClient, GetCommand } = require('@aws-sdk/lib-dynamodb');
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
        'Access-Control-Allow-Methods': 'GET,OPTIONS'
    };

    if (event.httpMethod === 'OPTIONS') {
        return { statusCode: 200, headers, body: '' };
    }

    try {
        // Verify JWT token
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
        
        // Use the username from JWT token (this matches our database userId)
        const userId = decoded['cognito:username'] || decoded.username;
        
        const result = await docClient.send(new GetCommand({
            TableName: process.env.USERS_TABLE_NAME || 'AIToadyUsers',
            Key: { userId }
        }));

        if (result.Item) {
            return {
                statusCode: 200,
                headers,
                body: JSON.stringify({ 
                    success: true, 
                    user: {
                        userId: result.Item.userId,
                        email: result.Item.email,
                        queriesRemaining: result.Item.queriesRemaining
                    }
                })
            };
        } else {
            return {
                statusCode: 404,
                headers,
                body: JSON.stringify({ success: false, error: 'User not found' })
            };
        }
    } catch (error) {
        console.error('Error:', error);
        return {
            statusCode: 500,
            headers,
            body: JSON.stringify({ success: false, error: 'Internal server error' })
        };
    }
};