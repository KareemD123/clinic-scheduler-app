export interface Invoice {
  id: string;
  appointmentId: string;
  patientId: string;
  patientName: string;
  amount: number;
  tax: number;
  totalAmount: number;
  status: string;
  items: InvoiceItem[];
  generatedAt: string;
  dueDate: string;
}

export interface InvoiceItem {
  description: string;
  amount: number;
}

export interface Payment {
  id: string;
  invoiceId: string;
  amount: number;
  paymentMethod: string;
  transactionId: string;
  status: string;
  processedAt: string;
  receipt?: PaymentReceipt;
}

export interface PaymentReceipt {
  receiptNumber: string;
  patientName: string;
  amount: number;
  paymentMethod: string;
  date: string;
}

export interface ProcessPaymentRequest {
  invoiceId: string;
  amount: number;
  paymentMethod: string;
  cardDetails?: {
    last4: string;
    brand: string;
  };
}
