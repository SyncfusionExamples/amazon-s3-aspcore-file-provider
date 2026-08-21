# amazon-s3-aspcore-file-provider

This repository contains the Amazon S3 bucket file system provider in ASP.NET Core for the File Manager component.

## Key Features

The following actions can be performed with Amazon S3 bucket file system provider:

| **Actions** | **Description** |
| --- | --- |
| Read         | Reads the files from Amazon S3 bucket. |
| Details      | Gets the file's details like Type, Size, Location, and Modified date. |
| Download     | Downloads the selected file or folder from the Amazon S3 bucket. |
| Upload       | Uploads a file to the Amazon S3 bucket. It accepts uploaded media with the following characteristics: <ul><li>Maximum file size:  30MB</li><li>Accepted Media MIME types: `*/*` </li></ul> |
| Create       | Creates a new folder. |
| Delete       | Deletes a folder or file. |
| Copy         | Copies the selected files from target. |
| Move         | Pastes the copied files to the desired location. |
| Rename       | Renames a folder or file. |
| Search       | Full-text queries perform linguistic searches against text data in full-text indexes by operating on words and phrases. |

## Prerequisites

To run the service, create an Amazon S3 bucket in one of the AWS Regions for accessing and storing the S3 objects as files or folders. Create an [Amazon S3 account](https://docs.aws.amazon.com/AmazonS3/latest/gsg/CreatingABucket.html) and then create S3 bucket to perform the file operations. Then, open the `AmazonS3FileProvider` and register your Amazon S3 client account details like awsAccessKeyId, awsSecretAccessKey, bucketRegion, and bucketName details in `RegisterAmazonS3` method to perform the file operations. 

```
  void RegisterAmazonS3(string bucketName, string awsAccessKeyId, string awsSecretAccessKey, string bucketRegion);
```

## How to run this application

To run this application, clone the [`amazon-s3-aspcore-file-provider`](https://github.com/SyncfusionExamples/amazon-s3-aspcore-file-provider) repository and then navigate to its appropriate path where it has been located in your system.

To do so, open the command prompt and run the following commands one after the other.

```
git clone https://github.com/SyncfusionExamples/amazon-s3-aspcore-file-provider

cd amazon-s3-aspcore-file-provider
```
## Restore the NuGet package and build the application

To restore the NuGet package, run the following command in root folder of the application.

```
dotnet restore
```

To build the application, run the following command.

```
dotnet build
```

## Running application

After successful compilation, run the following command to run the application.

```
dotnet run
```

Now, the project will be hosted in http://localhost. To ensure the Amazon-s3-service, map the following URL in your browser.

```
http://localhost:<port-number>/api/test
```

## File Manager AjaxSettings

To access the basic actions such as Read, Delete, Copy, Move, Rename, Search, and Get Details of File Manager using Amazon s3 service, just map the following code snippet in the Ajaxsettings property of File Manager.

Here, the `hostUrl` will be your locally hosted port number.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/AmazonS3Provider/AmazonS3FileOperations'
  }
```

## File download AjaxSettings

To perform download operation, initialize the `downloadUrl` property in ajaxSettings of the File Manager component.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/AmazonS3Provider/AmazonS3FileOperations',
        downloadUrl: hostUrl + 'api/AmazonS3Provider/AmazonS3Download'
  }
```

## File upload AjaxSettings

To perform upload operation, initialize the `uploadUrl` property in ajaxSettings of the File Manager component.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/AmazonS3Provider/AmazonS3FileOperations',
        uploadUrl: hostUrl + 'api/AmazonS3Provider/AmazonS3Upload'
  }
```

## File image preview AjaxSettings

To perform image preview support in the File Manager component, initialize the `getImageUrl` property in ajaxSettings of the File Manager component.

```
  var hostUrl = http://localhost:62870/;
  ajaxSettings: {
        url: hostUrl + 'api/AmazonS3Provider/AmazonS3FileOperations',
         getImageUrl: hostUrl + 'api/AmazonS3Provider/AmazonS3GetImage'
  }
```

The FileManager will be rendered as the following.

![File Manager](https://ej2.syncfusion.com/products/images/file-manager/readme.gif)

## Docker Support

The File Manager Amazon S3 file provider is also available as a pre-built Docker image for quick deployment to your infrastructure. The image exposes the same Web API endpoints as the ASP.NET Core project in this repository; configure it through the environment variables listed below.

You can deploy it quickly to your infrastructure. If you want to add new functionality or customize any existing functionalities, create your own Docker file by referencing the existing [File Manager Amazon S3 Docker project](https://github.com/SyncfusionExamples/amazon-s3-aspcore-file-provider).

The File Manager is supported on multiple platforms including JavaScript, Angular, React, Vue, ASP.NET Core, ASP.NET MVC, TypeScript, and Blazor.

### Prerequisites

Have [Docker](https://www.docker.com/products/container-runtime#/download) installed in your environment:

* On Windows, install [Docker for Windows](https://docs.docker.com/docker-for-windows/install/).
* On macOS, install [Docker for Mac](https://docs.docker.com/docker-for-mac/install/).

### How to deploy the File Manager Amazon S3 Service Docker Image

**Step 1:** Pull the Amazon S3 file provider image from Docker Hub.

```
docker pull syncfusion/filemanager-amazon-s3-aspnetcore-provider
```

**Step 2:** Create the `docker-compose.yml` file with the following content.

**docker-compose.yml**

```yaml
version: '3.8'

services:
  amazon-s3-aspnetcore-provider:
    image: syncfusion/filemanager-amazon-s3-aspnetcore-provider:latest
    environment:
      # Provide your Amazon S3 credentials
      AWS_ACCESS_KEY_ID: YOUR_AWS_ACCESS_KEY_ID
      AWS_SECRET_ACCESS_KEY: YOUR_AWS_SECRET_ACCESS_KEY
      AWS_BUCKET_NAME: YOUR_AWS_BUCKET_NAME
      AWS_BUCKET_REGION: YOUR_AWS_BUCKET_REGION
    ports:
      - "5000:80"
```

**Step 3:** Run the container.

In a terminal tab, navigate to the directory where you placed the `docker-compose.yml` file and execute the following:

```
docker compose up
```

The File Manager Amazon S3 provider will be accessible at `http://localhost:5000`.

To stop the container, run:

```
docker compose down
```

#### Amazon S3 credential details

| Environment Variable | Required | Description |
|----------------------|----------|-------------|
| `AWS_ACCESS_KEY_ID` | Yes | Access key ID of your AWS IAM user. |
| `AWS_SECRET_ACCESS_KEY` | Yes | Secret access key of your AWS IAM user. |
| `AWS_BUCKET_NAME` | Yes | Name of the S3 bucket that stores the files. |
| `AWS_BUCKET_REGION` | Yes | AWS region code where the bucket is hosted. Example: `us-east-1` |

**Step 4:** Configure the client-side File Manager component.

To access the file operations through the Amazon S3 service running in Docker, just map the following code snippet in the `ajaxSettings` property of the File Manager component. Here, the `hostUrl` will be the URL of the running Docker instance.

```
  var hostUrl = http://localhost:5000/;
  ajaxSettings: {
        url: hostUrl + 'api/AmazonS3Provider/AmazonS3FileOperations',
        uploadUrl: hostUrl + 'api/AmazonS3Provider/AmazonS3Upload',
        downloadUrl: hostUrl + 'api/AmazonS3Provider/AmazonS3Download',
        getImageUrl: hostUrl + 'api/AmazonS3Provider/AmazonS3GetImage'
  }
```

For more information about File Manager setup on other platforms, see the getting started pages for [JavaScript](https://help.syncfusion.com/file-manager-sdk/javascript/es5-getting-started), [Angular](https://help.syncfusion.com/file-manager-sdk/angular/getting-started), [React](https://help.syncfusion.com/file-manager-sdk/react/getting-started), [Vue](https://help.syncfusion.com/file-manager-sdk/vue/getting-started), [TypeScript](https://help.syncfusion.com/file-manager-sdk/typescript/getting-started), [ASP.NET Core](https://help.syncfusion.com/file-manager-sdk/asp-net-core/getting-started), [ASP.NET MVC](https://help.syncfusion.com/file-manager-sdk/asp-net-mvc/getting-started), and [Blazor](https://help.syncfusion.com/file-manager-sdk/blazor/getting-started-with-web-app).

## Support

Product support is available through the following mediums:

* Creating incident in Syncfusion [Direct-trac](https://www.syncfusion.com/support/directtrac/incidents?utm_source=npm&utm_campaign=filemanager) support system or [Community forum](https://www.syncfusion.com/forums/essential-js2?utm_source=npm&utm_campaign=filemanager).

* New [GitHub issue](https://github.com/syncfusion/ej2-javascript-ui-controls/issues/new).

* Ask your query in [Stack Overflow](https://stackoverflow.com/?utm_source=npm&utm_campaign=filemanager) with tag `syncfusion` and `ej2`.

## License

Check the license details [here](https://github.com/syncfusion/ej2-javascript-ui-controls/blob/master/license).

## Changelog

Check the changelog [here](https://github.com/syncfusion/ej2-javascript-ui-controls/blob/master/controls/filemanager/CHANGELOG.md)

© Copyright 2020 Syncfusion, Inc. All Rights Reserved. The Syncfusion Essential Studio license and copyright applies to this distribution.
