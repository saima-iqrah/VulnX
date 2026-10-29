export interface User {
    id: string;
    email: string;
    fullName: string;
    roles: string[];
    status: string;
    emailConfirmed: boolean;
    createdAtUtc: string;
    lastLoginAtUtc: string | null;
}

export interface AuthResponse {
    accessToken: string;
    accessTokenExpiresAtUtc: string;
    user: User;
}