/** 'Guest' = throwaway session minted for anonymous Solitary practice; practice-only. */
export type UserRole = 'Player' | 'Admin' | 'Guest';

export interface AuthResponse {
  token: string;
  userId: string;
  displayName: string;
  role: UserRole;
}
