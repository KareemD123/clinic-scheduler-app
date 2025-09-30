import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AppointmentService } from '../../../core/services/appointment.service';
import { PatientService } from '../../../core/services/patient.service';
import { DoctorService } from '../../../core/services/doctor.service';
import { PatientListItem } from '../../../core/models/patient.model';
import { DoctorListItem } from '../../../core/models/doctor.model';

@Component({
  selector: 'app-appointment-create',
  templateUrl: './appointment-create.component.html',
  styleUrls: ['./appointment-create.component.scss']
})
export class AppointmentCreateComponent implements OnInit {
  appointmentForm: FormGroup;
  patients: PatientListItem[] = [];
  doctors: DoctorListItem[] = [];
  loading = false;
  errorMessage = '';
  successMessage = '';
  invoiceId: string | null = null;

  constructor(
    private fb: FormBuilder,
    private appointmentService: AppointmentService,
    private patientService: PatientService,
    private doctorService: DoctorService,
    public router: Router
  ) {
    this.appointmentForm = this.fb.group({
      patientId: ['', Validators.required],
      doctorId: ['', Validators.required],
      appointmentDateTime: ['', Validators.required],
      duration: [30, [Validators.required, Validators.min(15)]],
      reason: ['', Validators.required],
      notes: [''],
      generateInvoice: [true]
    });
  }

  ngOnInit(): void {
    this.loadPatients();
    this.loadDoctors();
  }

  loadPatients(): void {
    this.patientService.getAllPatients().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.patients = response.data;
        }
      },
      error: (error) => console.error('Error loading patients:', error)
    });
  }

  loadDoctors(): void {
    this.doctorService.getAllDoctors().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.doctors = response.data;
        }
      },
      error: (error) => console.error('Error loading doctors:', error)
    });
  }

  onSubmit(): void {
    if (this.appointmentForm.invalid) {
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.successMessage = '';

    const formValue = this.appointmentForm.value;
    const request = {
      patientId: formValue.patientId,
      doctorId: formValue.doctorId,
      appointmentDateTime: new Date(formValue.appointmentDateTime).toISOString(),
      duration: formValue.duration,
      reason: formValue.reason,
      generateInvoice: formValue.generateInvoice,
      processPayment: false
    };

    this.appointmentService.scheduleAndBill(request).subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.successMessage = 'Appointment scheduled successfully!';
          if (response.data.invoice) {
            this.invoiceId = response.data.invoice.id;
          }
        } else {
          this.errorMessage = response.message || 'Failed to schedule appointment';
        }
        this.loading = false;
      },
      error: (error) => {
        this.errorMessage = 'An error occurred while scheduling the appointment';
        this.loading = false;
        console.error('Error:', error);
      }
    });
  }

  proceedToPayment(): void {
    if (this.invoiceId) {
      this.router.navigate(['/billing/payment', this.invoiceId]);
    }
  }
}
