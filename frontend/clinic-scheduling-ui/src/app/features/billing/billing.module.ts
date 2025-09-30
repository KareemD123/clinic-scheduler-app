import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

import { BillingRoutingModule } from './billing-routing.module';
import { PaymentProcessComponent } from './payment-process/payment-process.component';
import { InvoiceViewComponent } from './invoice-view/invoice-view.component';


@NgModule({
  declarations: [
    PaymentProcessComponent,
    InvoiceViewComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    BillingRoutingModule
  ]
})
export class BillingModule { }
