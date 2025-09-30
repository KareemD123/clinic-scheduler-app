import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { 
  Appointment, 
  CreateAppointmentRequest, 
  ScheduleAndBillRequest, 
  ScheduleAndBillResponse 
} from '../models/appointment.model';

@Injectable({
  providedIn: 'root'
})
export class AppointmentService {
  private apiUrl = `${environment.apiUrl}/appointments`;

  constructor(private http: HttpClient) { }

  createAppointment(request: CreateAppointmentRequest): Observable<ApiResponse<Appointment>> {
    return this.http.post<ApiResponse<Appointment>>(this.apiUrl, request);
  }

  getAllAppointments(): Observable<ApiResponse<Appointment[]>> {
    return this.http.get<ApiResponse<Appointment[]>>(this.apiUrl);
  }

  getAppointmentById(id: string): Observable<ApiResponse<Appointment>> {
    return this.http.get<ApiResponse<Appointment>>(`${this.apiUrl}/${id}`);
  }

  scheduleAndBill(request: ScheduleAndBillRequest): Observable<ApiResponse<ScheduleAndBillResponse>> {
    return this.http.post<ApiResponse<ScheduleAndBillResponse>>(`${this.apiUrl}/schedule-and-bill`, request);
  }
}
