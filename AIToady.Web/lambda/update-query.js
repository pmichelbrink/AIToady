const { DynamoDBClient } = require('@aws-sdk/client-dynamodb');
const { DynamoDBDocumentClient, UpdateCommand } = require('@aws-sdk/lib-dynamodb');
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
        await verifyToken(token);
        
        const body = JSON.parse(event.body);
        const { queryId, updates } = body;
        
        if (!queryId) {
            return {
                statusCode: 400,
                headers,
                body: JSON.stringify({ error: 'queryId is required' })
            };
        }

        if (!updates || typeof updates !== 'object' || Object.keys(updates).length === 0) {
            return {
                statusCode: 400,
                headers,
                body: JSON.stringify({ error: 'updates object is required' })
            };
        }

        // Build dynamic update expression
        const updateExpressions = [];
        const expressionAttributeValues = {};
        
        Object.keys(updates).forEach((key, index) => {
            updateExpressions.push(`${key} = :val${index}`);
            expressionAttributeValues[`:val${index}`] = updates[key];
        });

        await docClient.send(new UpdateCommand({
            TableName: process.env.QUERIES_TABLE_NAME || 'AIToadyQueries',
            Key: { QueryId: queryId },
            UpdateExpression: `SET ${updateExpressions.join(', ')}`,
            ExpressionAttributeValues: expressionAttributeValues
        }));

        return {
            statusCode: 200,
            headers,
            body: JSON.stringify({ 
                success: true
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
