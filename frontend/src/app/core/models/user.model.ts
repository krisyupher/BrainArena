export type UserRole = 'Player' | 'Admin';

export interface AuthResponse {
  token: string;
  userId: string;
  displayName: string;
  role: UserRole;
}
