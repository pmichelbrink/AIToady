import { fetchAuthSession } from 'aws-amplify/auth';

export interface UserRecord {
  userId: string;
  email: string;
  queriesRemaining: number;
  createdAt?: string;
  updatedAt?: string;
}

export const userService = {
  async getUser(): Promise<{ success: boolean; user?: UserRecord; error?: string }> {
    try {
      const apiUrl = import.meta.env.VITE_API_URL;
      if (!apiUrl) {
        return { success: false, error: 'API URL not configured' };
      }

      // Get JWT token from Cognito
      const session = await fetchAuthSession();
      const token = session.tokens?.idToken?.toString();
      
      if (!token) {
        return { success: false, error: 'No authentication token' };
      }

      const response = await fetch(`${apiUrl}/user`, {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      
      const data = await response.json();
      
      if (response.ok && data.success) {
        return { success: true, user: data.user };
      } else {
        return { success: false, error: data.error || 'Failed to get user' };
      }
    } catch (error: any) {
      return { success: false, error: error.message || 'Failed to get user' };
    }
  }
};