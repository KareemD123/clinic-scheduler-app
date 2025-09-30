import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PaymentProcessComponent } from './payment-process/payment-process.component';
import { InvoiceViewComponent } from './invoice-view/invoice-view.component';

const routes: Routes = [
  { path: 'payment/:id', component: PaymentProcessComponent },
  { path: 'invoice/:id', component: InvoiceViewComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class BillingRoutingModule { }
