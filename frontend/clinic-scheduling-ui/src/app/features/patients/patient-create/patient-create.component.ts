import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PatientService } from '../../../core/services/patient.service';

@Component({
  selector: 'app-patient-create',
  templateUrl: './patient-create.component.html',
  styleUrls: ['./patient-create.component.scss']
})
export class PatientCreateComponent {
  patientForm: FormGroup;
  loading = false;
  errorMessage = '';

  constructor(
    private fb: FormBuilder,
    private patientService: PatientService,
    private router: Router
  ) {
    this.patientForm = this.fb.group({
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      dateOfBirth: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      phone: ['', Validators.required],
      address: this.fb.group({
        street: ['', Validators.required],
        city: ['', Validators.required],
        state: ['', Validators.required],
        zipCode: ['', Validators.required]
      })
    });
  }

  onSubmit(): void {
    if (this.patientForm.invalid) {
      return;
    }

    this.loading = true;
    this.errorMessage = '';

    this.patientService.createPatient(this.patientForm.value).subscribe({
      next: (response) => {
        if (response.success) {
          alert('Patient created successfully!');
          this.router.navigate(['/appointments/create']);
        } else {
          this.errorMessage = response.message || 'Failed to create patient';
        }
        this.loading = false;
      },
      error: (error) => {
        this.errorMessage = 'An error occurred while creating the patient';
        this.loading = false;
        console.error('Error:', error);
      }
    });
  }
}
