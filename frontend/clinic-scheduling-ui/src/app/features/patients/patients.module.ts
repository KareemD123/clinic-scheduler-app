import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

import { PatientsRoutingModule } from './patients-routing.module';
import { PatientCreateComponent } from './patient-create/patient-create.component';
import { PatientListComponent } from './patient-list/patient-list.component';


@NgModule({
  declarations: [
    PatientCreateComponent,
    PatientListComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PatientsRoutingModule
  ]
})
export class PatientsModule { }
