import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { BillingService } from '../../../core/services/billing.service';
import { Invoice, Payment } from '../../../core/models/billing.model';

@Component({
  selector: 'app-payment-process',
  templateUrl: './payment-process.component.html',
  styleUrls: ['./payment-process.component.scss']
})
export class PaymentProcessComponent implements OnInit {
  invoiceId: string = '';
  invoice: Invoice | null = null;
  paymentForm: FormGroup;
  loading = false;
  loadingInvoice = true;
  errorMessage = '';
  payment: Payment | null = null;

  constructor(
    private route: ActivatedRoute,
    public router: Router,
    private fb: FormBuilder,
    private billingService: BillingService
  ) {
    this.paymentForm = this.fb.group({
      paymentMethod: ['credit_card', Validators.required],
      cardBrand: ['Visa', Validators.required],
      cardLast4: ['4242', [Validators.required, Validators.pattern(/^\d{4}$/)]]
    });
  }

  ngOnInit(): void {
    this.invoiceId = this.route.snapshot.paramMap.get('id') || '';
    if (this.invoiceId) {
      this.loadInvoice();
    }
  }

  loadInvoice(): void {
    this.billingService.getInvoiceById(this.invoiceId).subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.invoice = response.data;
        } else {
          this.errorMessage = 'Invoice not found';
        }
        this.loadingInvoice = false;
      },
      error: (error) => {
        this.errorMessage = 'Error loading invoice';
        this.loadingInvoice = false;
        console.error('Error:', error);
      }
    });
  }

  onSubmit(): void {
    if (this.paymentForm.invalid || !this.invoice) {
      return;
    }

    this.loading = true;
    this.errorMessage = '';

    const formValue = this.paymentForm.value;
    const request = {
      invoiceId: this.invoiceId,
      amount: this.invoice.totalAmount,
      paymentMethod: formValue.paymentMethod,
      cardDetails: {
        last4: formValue.cardLast4,
        brand: formValue.cardBrand
      }
    };

    this.billingService.processPayment(request).subscribe({
      next: (response) => {
        if (response.success && response.data) {
          this.payment = response.data;
        } else {
          this.errorMessage = response.message || 'Payment processing failed';
        }
        this.loading = false;
      },
      error: (error) => {
        this.errorMessage = 'An error occurred while processing payment';
        this.loading = false;
        console.error('Error:', error);
      }
    });
  }
}
