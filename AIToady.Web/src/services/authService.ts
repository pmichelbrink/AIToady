import { signUp, confirmSignUp, signIn, resetPassword, confirmResetPassword } from 'aws-amplify/auth';

export interface SignUpData {
  email: string;
  password: string;
}

import { getCurrentUser, signOut } from 'aws-amplify/auth';

export const authService = {
  async getCurrentUser() {
    try {
      const user = await getCurrentUser();
      return { success: true, user };
    } catch (error) {
      return { success: false, error: 'No authenticated user' };
    }
  },

  async signOut() {
    try {
      await signOut();
      return { success: true };
    } catch (error: any) {
      return { success: false, error: error.message || 'Sign out failed' };
    }
  },
  async signUp({ email, password }: SignUpData) {
    try {
      const username = `user_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
      const { isSignUpComplete, userId, nextStep } = await signUp({
        username,
        password,
        options: {
          userAttributes: {
            email
          }
        }
      });
      
      return { 
        success: true, 
        isSignUpComplete, 
        userId, 
        nextStep,
        username,
        message: 'Sign up successful! Check your email for verification code.' 
      };
    } catch (error: any) {
      return { 
        success: false, 
        error: error.message || 'Sign up failed' 
      };
    }
  },

  async confirmSignUp(username: string, confirmationCode: string) {
    try {
      await confirmSignUp({
        username,
        confirmationCode
      });
      
      return { success: true, message: 'Account confirmed successfully!' };
    } catch (error: any) {
      return { success: false, error: error.message || 'Confirmation failed' };
    }
  },

  async signIn(email: string, password: string) {
    try {
      const { isSignedIn, nextStep } = await signIn({
        username: email,
        password
      });
      return { success: true, isSignedIn, nextStep };
    } catch (error: any) {
      return { success: false, error: error.message || 'Sign in failed' };
    }
  },

  async resetPassword(email: string) {
    try {
      await resetPassword({ username: email });
      return { success: true, message: 'Password reset code sent to your email' };
    } catch (error: any) {
      return { success: false, error: error.message || 'Password reset failed' };
    }
  },

  async confirmResetPassword(email: string, confirmationCode: string, newPassword: string) {
    try {
      await confirmResetPassword({
        username: email,
        confirmationCode,
        newPassword
      });
      return { success: true, message: 'Password reset successfully!' };
    } catch (error: any) {
      return { success: false, error: error.message || 'Password reset confirmation failed' };
    }
  }
};