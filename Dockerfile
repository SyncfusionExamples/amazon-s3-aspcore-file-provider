# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:2.1 AS build
WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Run the application
FROM mcr.microsoft.com/dotnet/aspnet:2.1
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EJ2AmazonS3ASPCoreFileProvider.dll"]
