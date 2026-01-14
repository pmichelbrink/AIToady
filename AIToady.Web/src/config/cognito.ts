import { Amplify } from 'aws-amplify';

const cognitoConfig = {
  Auth: {
    Cognito: {
      userPoolId: import.meta.env.VITE_USER_POOL_ID || 'YOUR_USER_POOL_ID',
      userPoolClientId: import.meta.env.VITE_USER_POOL_CLIENT_ID || 'YOUR_USER_POOL_CLIENT_ID',
      region: import.meta.env.VITE_AWS_REGION || 'YOUR_AWS_REGION'
    }
  }
};

Amplify.configure(cognitoConfig);

export default cognitoConfig;