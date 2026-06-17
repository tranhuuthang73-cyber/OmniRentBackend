appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=mylocal;Database=OmniRentDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "Authentication": {
    "Google": {
      "ClientId": "apikeyGG",
      "ClientSecret": "apikeyGG"
    }
  },
  "GeminiAI": {
    "ApiKey": "apikeyGG"
  }
}

appsetting.development.json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft.AspNetCore": "Information"
        }
    },
  "ConnectionStrings": {
    "DefaultConnection": "Server=mylocal;Database=OmniRentDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
