import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService, DevTokenRequest } from '../../core/services/auth.service';

interface RoleOption {
  value: string;
  label: string;
}

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login.page.html',
  styleUrl: './login.page.css',
})
export class LoginPageComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly roleOptions: RoleOption[] = [
    { value: 'Admin', label: 'Admin (full access)' },
    { value: 'Physician', label: 'Physician' },
    { value: 'Physicist', label: 'Physicist' },
    { value: 'Physicist', label: 'Physicist' },
    { value: 'Dosimetrist', label: 'Dosimetrist' },
    { value: 'SimTech', label: 'SimTech' },
    { value: 'Scheduler', label: 'Scheduler' },
  ];

  userId = 'dev-user';
  role = 'Admin';
  error = '';
  loading = false;

  login(): void {
    if (!this.userId.trim()) {
      this.error = 'User ID is required.';
      return;
    }

    this.loading = true;
    this.error = '';

    const request: DevTokenRequest = {
      userId: this.userId.trim(),
      displayName: `${this.userId.trim()} (Dev)`,
      role: this.role,
    };

    this.authService.requestDevToken(request).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/']);
      },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.error ?? err.message ?? 'Login failed.';
      },
    });
  }
}
