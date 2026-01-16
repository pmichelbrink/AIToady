import { fetchAuthSession } from 'aws-amplify/auth';

export const queryService = {
  async createQuery(queryText: string): Promise<{ success: boolean; queryId?: string; error?: string }> {
    try {
      const apiUrl = import.meta.env.VITE_API_URL;
      if (!apiUrl) {
        return { success: false, error: 'API URL not configured' };
      }

      const session = await fetchAuthSession();
      const token = session.tokens?.idToken?.toString();
      
      if (!token) {
        return { success: false, error: 'No authentication token' };
      }

      const response = await fetch(`${apiUrl}/query`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ queryText })
      });
      
      const data = await response.json();
      
      if (response.ok && data.success) {
        return { success: true, queryId: data.queryId };
      } else {
        return { success: false, error: data.error || 'Failed to create query' };
      }
    } catch (error: any) {
      return { success: false, error: error.message || 'Failed to create query' };
    }
  }
};
