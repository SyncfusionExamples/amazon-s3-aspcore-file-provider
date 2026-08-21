FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

RUN apt-get update -y && apt-get install fontconfig -y
WORKDIR /app

ENV AWS_ACCESS_KEY_ID=""
ENV AWS_SECRET_ACCESS_KEY=""
ENV AWS_BUCKET_NAME=""
ENV AWS_BUCKET_REGION=""

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /source
COPY ["src/AmazonS3ASPCoreFileProvider/AmazonS3ASPCoreFileProvider.csproj", "./AmazonS3ASPCoreFileProvider/"]
COPY ["src/AmazonS3ASPCoreFileProvider/NuGet.Config", "./AmazonS3ASPCoreFileProvider/"]
RUN dotnet restore "./AmazonS3ASPCoreFileProvider/AmazonS3ASPCoreFileProvider.csproj"
COPY . .
WORKDIR "/source/src"
RUN dotnet build -c Release -f net10.0 -o /app

FROM build AS publish
RUN dotnet publish -c Release -f net10.0 -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_HTTP_PORTS=80
ENTRYPOINT ["dotnet", "AmazonS3ASPCoreFileProvider.dll"]
