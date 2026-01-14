export interface UserRecord {
  userId: string;
  email: string;
  queriesRemaining: number;
  createdAt?: string;
  updatedAt?: string;
}

export const userService = {
  async getUser(userId: string): Promise<{ success: boolean; user?: UserRecord; error?: string }> {
    try {
      const apiUrl = import.meta.env.VITE_API_URL;
      console.log('API URL:', apiUrl);
      console.log('User ID:', userId);
      
      if (!apiUrl) {
        return { success: false, error: 'API URL not configured' };
      }

      const url = `${apiUrl}/users/${userId}`;
      console.log('Fetching:', url);
      
      const response = await fetch(url);
      const data = await response.json();
      
      console.log('Response status:', response.status);
      console.log('Response data:', data);
      
      if (response.ok && data.success) {
        return { success: true, user: data.user };
      } else {
        return { success: false, error: data.error || 'Failed to get user' };
      }
    } catch (error: any) {
      console.error('API Error:', error);
      return { success: false, error: error.message || 'Failed to get user' };
    }
  }
};