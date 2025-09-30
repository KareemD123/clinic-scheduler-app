import { Component, OnInit } from '@angular/core';
import { AppointmentService } from '../../../core/services/appointment.service';
import { Appointment } from '../../../core/models/appointment.model';

@Component({
  selector: 'app-appointment-list',
  templateUrl: './appointment-list.component.html',
  styleUrls: ['./appointment-list.component.scss']
})
export class AppointmentListComponent implements OnInit {
  appointments: Appointment[] = [];
  loading = true;
  errorMessage = '';

  constructor(private appointmentService: AppointmentService) { }

  ngOnInit(): void {
    this.loadAppointments();
  }

  loadAppointments(): void {
    this.appointmentService.getAllAppointments().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.appointments = response.data;
        } else {
          this.errorMessage = response.message || 'Failed to load appointments';
        }
        this.loading = false;
      },
      error: (error) => {
        this.errorMessage = 'An error occurred while loading appointments';
        this.loading = false;
        console.error('Error:', error);
      }
    });
  }

  getStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'scheduled': return 'status-scheduled';
      case 'completed': return 'status-completed';
      case 'cancelled': return 'status-cancelled';
      case 'noshow': return 'status-no-show';
      default: return 'status-unknown';
    }
  }

  formatDateTime(dateTime: string): string {
    return new Date(dateTime).toLocaleString();
  }
}
