export interface Doctor {
  id: string;
  firstName: string;
  lastName: string;
  specialization: string;
  email: string;
  phone: string;
  consultationFee: number;
  availability: DoctorAvailability[];
  createdAt: string;
}

export interface DoctorAvailability {
  dayOfWeek: number;
  startTime: string;
  endTime: string;
}

export interface DoctorListItem {
  id: string;
  firstName: string;
  lastName: string;
  specialization: string;
  consultationFee: number;
}
