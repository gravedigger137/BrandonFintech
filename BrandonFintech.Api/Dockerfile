# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY BrandonFintech.sln ./
COPY BrandonFintech.Api/BrandonFintech.Api.csproj BrandonFintech.Api/
COPY BrandonFintech.Accounts/BrandonFintech.Accounts.csproj BrandonFintech.Accounts/
COPY BrandonFintech.Admin/BrandonFintech.Admin.csproj BrandonFintech.Admin/
COPY BrandonFintech.Audit/BrandonFintech.Audit.csproj BrandonFintech.Audit/
COPY BrandonFintech.Common/BrandonFintech.Common.csproj BrandonFintech.Common/
COPY BrandonFintech.Contracts/BrandonFintech.Contracts.csproj BrandonFintech.Contracts/
COPY BrandonFintech.Identity/BrandonFintech.Identity.csproj BrandonFintech.Identity/
COPY BrandonFintech.Infrastructure/BrandonFintech.Infrastructure.csproj BrandonFintech.Infrastructure/
COPY BrandonFintech.Ledger/BrandonFintech.Ledger.csproj BrandonFintech.Ledger/
COPY BrandonFintech.Payments/BrandonFintech.Payments.csproj BrandonFintech.Payments/
COPY BrandonFintech.Transfers/BrandonFintech.Transfers.csproj BrandonFintech.Transfers/

RUN dotnet restore BrandonFintech.Api/BrandonFintech.Api.csproj

COPY . .

RUN dotnet publish BrandonFintech.Api/BrandonFintech.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BrandonFintech.Api.dll"]
