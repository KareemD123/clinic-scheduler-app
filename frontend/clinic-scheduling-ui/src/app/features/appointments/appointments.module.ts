import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

import { AppointmentsRoutingModule } from './appointments-routing.module';
import { AppointmentCreateComponent } from './appointment-create/appointment-create.component';
import { AppointmentListComponent } from './appointment-list/appointment-list.component';


@NgModule({
  declarations: [
    AppointmentCreateComponent,
    AppointmentListComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppointmentsRoutingModule
  ]
})
export class AppointmentsModule { }
