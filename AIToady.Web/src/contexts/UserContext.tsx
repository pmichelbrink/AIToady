import { createContext, useContext } from 'react';

interface UserContextType {
  userEmail: string | null;
  queriesRemaining: number | null;
  showToast: (message: string) => void;
}

export const UserContext = createContext<UserContextType>({
  userEmail: null,
  queriesRemaining: null,
  showToast: () => {}
});

export const useUser = () => useContext(UserContext);