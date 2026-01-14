import { createContext, useContext } from 'react';

interface UserContextType {
  userEmail: string | null;
  queriesRemaining: number | null;
}

export const UserContext = createContext<UserContextType>({
  userEmail: null,
  queriesRemaining: null
});

export const useUser = () => useContext(UserContext);