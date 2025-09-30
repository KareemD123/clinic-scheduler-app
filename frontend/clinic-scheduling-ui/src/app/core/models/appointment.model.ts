import { Invoice, Payment } from './billing.model';

export interface Appointment {
  id: string;
  patientId: string;
  patientName: string;
  doctorId: string;
  doctorName: string;
  appointmentDateTime: string;
  duration: number;
  status: string;
  reason: string;
  notes: string;
  createdAt: string;
}

export interface CreateAppointmentRequest {
  patientId: string;
  doctorId: string;
  appointmentDateTime: string;
  duration: number;
  reason: string;
  notes?: string;
}

export interface ScheduleAndBillRequest {
  patientId: string;
  doctorId: string;
  appointmentDateTime: string;
  duration: number;
  reason: string;
  generateInvoice: boolean;
  processPayment: boolean;
}

export interface ScheduleAndBillResponse {
  appointment: Appointment;
  invoice?: Invoice;
  payment?: Payment;
}
