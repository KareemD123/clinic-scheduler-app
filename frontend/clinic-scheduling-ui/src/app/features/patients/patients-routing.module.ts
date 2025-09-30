import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PatientCreateComponent } from './patient-create/patient-create.component';
import { PatientListComponent } from './patient-list/patient-list.component';

const routes: Routes = [
  { path: 'create', component: PatientCreateComponent },
  { path: 'list', component: PatientListComponent },
  { path: '', redirectTo: 'create', pathMatch: 'full' }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class PatientsRoutingModule { }
