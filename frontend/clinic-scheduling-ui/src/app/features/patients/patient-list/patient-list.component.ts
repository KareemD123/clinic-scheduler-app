import { Component, OnInit } from '@angular/core';
import { PatientService } from '../../../core/services/patient.service';
import { PatientListItem } from '../../../core/models/patient.model';

@Component({
  selector: 'app-patient-list',
  templateUrl: './patient-list.component.html',
  styleUrls: ['./patient-list.component.scss']
})
export class PatientListComponent implements OnInit {
  patients: PatientListItem[] = [];
  loading = true;
  errorMessage = '';

  constructor(private patientService: PatientService) { }

  ngOnInit(): void {
    this.loadPatients();
  }

  loadPatients(): void {
    this.patientService.getAllPatients().subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.patients = response.data;
        } else {
          this.errorMessage = response.message || 'Failed to load patients';
        }
        this.loading = false;
      },
      error: (error) => {
        this.errorMessage = 'An error occurred while loading patients';
        this.loading = false;
        console.error('Error:', error);
      }
    });
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString();
  }
}
