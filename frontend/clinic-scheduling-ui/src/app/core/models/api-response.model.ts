export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
  errors?: ValidationError[];
  errorCode?: string;
  count?: number;
}

export interface ValidationError {
  field: string;
  message: string;
}
