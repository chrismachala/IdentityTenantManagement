# Identity Tenant Management - Blazor App

A Blazor Server application for onboarding organizations and their administrator users.

## Features

- Organization onboarding form with validation
- User-friendly interface with Bootstrap styling
- Real-time form validation
- Success/error feedback
- Integration with IdentityTenantManagement API

## Prerequisites

- .NET 9.0 SDK
- Running instance of IdentityTenantManagement API

## Configuration

Update the API base URL in `appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

Change the `BaseUrl` to match your IdentityTenantManagement API endpoint.

## Running the Application

1. Ensure the IdentityTenantManagement API is running
2. Navigate to the BlazorApp directory:
   ```bash
   cd IdentityTenantManagement.BlazorApp
   ```
3. Run the application:
   ```bash
   dotnet run
   ```
4. Open your browser and navigate to the URL shown in the console (typically `https://localhost:5XXX`)

## Using the Onboarding Form

1. Click on "Onboarding" in the navigation menu
2. Fill in the organization details:
   - Organization Name
   - Domain
3. Fill in the administrator account details:
   - First Name
   - Last Name
   - Username
   - Email Address
   - Password
4. Click "Complete Onboarding"
5. Upon success, you'll see a confirmation message and be redirected to the home page

## Project Structure

```
IdentityTenantManagement.BlazorApp/
├── Components/
│   ├── Layout/          # Layout components
│   └── Pages/
│       └── Onboarding.razor  # Main onboarding form
├── Services/
│   └── OnboardingApiClient.cs  # API client for onboarding
├── Program.cs           # Application configuration
└── appsettings.json     # Configuration settings
```

## Development

The application uses:
- Blazor Server with Interactive Server rendering
- Bootstrap 5 for styling
- EditForm with DataAnnotations for validation
- HttpClient for API communication

## API Integration

The app communicates with the following API endpoint:

- **POST** `/api/Onboarding/OnboardOrganisation`
  - Accepts: `TenantUserOnboardingModel`
  - Returns: Success message or error

## Troubleshooting

### Cannot connect to API
- Ensure the API is running
- Check the `BaseUrl` in `appsettings.json`
- Verify CORS settings on the API if running on different domains

### Form validation errors
- All fields are required
- Email must be a valid email address format
- Password should be at least 8 characters (API may have additional requirements)