using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Syncfusion.EJ2.FileManager.Base;
using System.IO;
using Microsoft.AspNetCore.Http;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Text.Json;

namespace Syncfusion.EJ2.FileManager.AmazonS3FileProvider
{
    public class AmazonS3FileProvider
    {
        protected static string bucketName;
        static IAmazonS3 client;
        static ListObjectsResponse response;
        static ListObjectsResponse childResponse;
        public string RootName;
        private string rootName = string.Empty;
        private string accessMessage = string.Empty;
        AccessDetails AccessDetails = new AccessDetails();
        long sizeValue = 0;
        List<FileManagerDirectoryContent> s3ObjectFiles = new List<FileManagerDirectoryContent>();
        TransferUtility fileTransferUtility = new TransferUtility(client);
        private static List<PartETag> partETags;
        private static string uploadId;

        // Register the amazon client details
        public void RegisterAmazonS3(string name, string awsAccessKeyId, string awsSecretAccessKey, string region)
        {
            bucketName = name;
            RegionEndpoint bucketRegion = RegionEndpoint.GetBySystemName(region);
            client = new AmazonS3Client(awsAccessKeyId, awsSecretAccessKey, bucketRegion);
            GetBucketList();
        }

        //Define the root directory to the file manager
        public void GetBucketList()
        {
            ListingObjectsAsync("", "", false).Wait();
            RootName = response.S3Objects.Where(x => x.Key.Split(".").Length != 2).First().Key;
            RootName = RootName.Replace("../", "");
        }
        public void SetRules(AccessDetails details)
        {
            this.AccessDetails = details;
            DirectoryInfo root = new DirectoryInfo(RootName);
            this.rootName = root.ToString();
        }

        // Reads the file(s) and folder(s)
        public FileManagerResponse GetFiles(string path, bool showHiddenItems, params FileManagerDirectoryContent[] data)
        {
            FileManagerDirectoryContent cwd = new FileManagerDirectoryContent();
            List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
            List<FileManagerDirectoryContent> filesS3 = new List<FileManagerDirectoryContent>();
            FileManagerResponse readResponse = new FileManagerResponse();
            GetBucketList();
            try
            {
                if (path == "/") ListingObjectsAsync("/", RootName , false).Wait(); else ListingObjectsAsync("/", this.RootName.Replace("/", "") + path, false).Wait();
                if (path == "/")
                {
                    FileManagerDirectoryContent[] s = response.S3Objects.Where(x => x.Key == RootName).Select(y => CreateDirectoryContentInstance(y.Key.ToString().Replace("/", ""), false, "Folder", y.Size, y.LastModified, y.LastModified, this.checkChild(y.Key), string.Empty)).ToArray();
                    if (s.Length > 0) cwd = s[0];
                }
                else
                    cwd = CreateDirectoryContentInstance(path.Split("/")[path.Split("/").Length - 2], false, "Folder", 0, DateTime.Now, DateTime.Now, (response.CommonPrefixes.Count > 0) ? true : false, path.Substring(0, path.IndexOf(path.Split("/")[path.Split("/").Length - 2])));
            }
            catch (Exception ex) { throw ex; }
            try
            {
                if (response.CommonPrefixes.Count > 0) {
                    files = response.CommonPrefixes.Select((y, i) => CreateDirectoryContentInstance(getFileName(response.CommonPrefixes[i], path), false, "Folder", 0, DateTime.Now, DateTime.Now, this.checkChild(response.CommonPrefixes[i]), getFilePath(y))).ToList();
                }
            }
            catch (Exception ex) { throw ex; }
            try
            {
                if (path == "/") ListingObjectsAsync("/", RootName, false).Wait(); else ListingObjectsAsync("/", this.RootName.Replace("/", "") + path, false).Wait();
                if (response.S3Objects.Count > 0)
                    filesS3 = response.S3Objects.Where(x => x.Key != RootName.Replace("/", "") + path).Select(y => CreateDirectoryContentInstance(y.Key.ToString().Replace(RootName.Replace("/", "") + path, "").Replace("/", ""), true, Path.GetExtension(y.Key.ToString()), y.Size, y.LastModified, y.LastModified, this.checkChild(y.Key), getFilterPath(y.Key, path))).ToList();
            }
            catch (Exception ex) { throw ex; }
            if (filesS3.Count != 0) files = files.Union(filesS3).ToList();
            readResponse.CWD = cwd;
            try
            {
                if ((cwd.Permission != null && !cwd.Permission.Read))
                {
                    readResponse.Files = null;
                    accessMessage = cwd.Permission.Message;
                    throw new UnauthorizedAccessException("'" + cwd.Name + "' is not accessible. You need permission to perform the read action.");
                }
            }
            catch (Exception e)
            {
                ErrorDetails er = new ErrorDetails();
                er.Message = e.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                readResponse.Error = er;
                return readResponse;
            }
            readResponse.Files = files;
            return readResponse;
        }

        private string getFilePath(string pathString)
        {
            return pathString.Substring(0, pathString.Length - pathString.Split("/")[pathString.Split("/").Length - 2].Length - 1).Substring(RootName.Length - 1);
        }

        private string getFileName(string fileName, string path)
        {
            return fileName.Replace(RootName.Replace("/", "") + path, "").Replace("/", "");
        }

        private FileManagerDirectoryContent CreateDirectoryContentInstance(string name, bool value, string type, long size, DateTime createddate, DateTime modifieddate, bool child, string filterpath)
        {
            FileManagerDirectoryContent tempFile = new FileManagerDirectoryContent();
            tempFile.Name = name;
            tempFile.IsFile = value;
            tempFile.Type = type;
            tempFile.Size = size;
            tempFile.DateCreated = createddate;
            tempFile.DateModified = modifieddate;
            tempFile.HasChild = child;
            tempFile.FilterPath = filterpath;
            tempFile.Permission= GetPathPermission(filterpath + (value ? name :Path.GetFileNameWithoutExtension(name)), value);
            return tempFile;
        }


        // Deletes file(s) or folder(s)
        public FileManagerResponse Delete(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            return AsyncDelete(path, names, data).Result;
        }

        // Delete aync method
        public virtual async Task<FileManagerResponse> AsyncDelete(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse removeResponse = new FileManagerResponse();
            try
            {
                List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
                GetBucketList();
                if (path == "/") ListingObjectsAsync("/", RootName , false).Wait(); else ListingObjectsAsync("/", this.RootName.Replace("/", "") + path, false).Wait();
                foreach (string name in names)
                {
                    foreach (FileManagerDirectoryContent item in data) {
                        AccessPermission PathPermission = GetPathPermission(item.FilterPath + item.Name, data[0].IsFile);
                        if (PathPermission != null && (!PathPermission.Read || !PathPermission.Write))
                        {
                            accessMessage = PathPermission.Message;
                            throw new UnauthorizedAccessException("'" + name + "' is not accessible.  You need permission to perform the write action.");
                        }
                    }
                    if (response.CommonPrefixes.Count > 1)
                    {
                        foreach (string commonPrefix in response.CommonPrefixes)
                        {
                            if (commonPrefix == this.RootName.Replace("/", "") + path + name)
                                files.Add(CreateDirectoryContentInstance(commonPrefix.Split("/")[commonPrefix.Split("/").Length - 2], false, "Folder", 0, DateTime.Now, DateTime.Now, false, ""));
                        }
                    }
                    if (response.S3Objects.Count > 1)
                    {
                        foreach (S3Object S3Object in response.S3Objects)
                        {
                            if (S3Object.Key == this.RootName.Replace("/", "") + path + name)
                                files.Add(CreateDirectoryContentInstance(S3Object.Key.Split("/").Last(), true, Path.GetExtension(S3Object.Key), S3Object.Size, S3Object.LastModified, S3Object.LastModified, false, ""));
                        }
                    }
                }
                await DeleteDirectory(path, names);
                removeResponse.Files = files;
                return removeResponse;
            }
            catch (Exception ex) {
                ErrorDetails er = new ErrorDetails();
                er.Message = ex.Message.ToString();
                er.Code = er.Message.Contains(" is not accessible.  You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                removeResponse.Error = er;
                return removeResponse;
            }
        }

        // Copy the file(s) or folder(s) from a source directory and pastes in given target directory
        public FileManagerResponse Copy(string path, string targetPath, string[] names, string[] replacedItemNames, FileManagerDirectoryContent TargetData, params FileManagerDirectoryContent[] data)
        {
            return this.TransferItems(path, targetPath, names, replacedItemNames, false, TargetData, data);
        }

        // Cut the file(s) or folder(s) from a source directory and pastes in given target directory
        public FileManagerResponse Move(string path, string targetPath, string[] names, string[] replacedItemNames, FileManagerDirectoryContent TargetData, params FileManagerDirectoryContent[] data)
        {
            return this.TransferItems(path, targetPath, names, replacedItemNames, true, TargetData, data);
        }

        // Cut or Copy the file(s) or folder(s) from a source directory and pastes in given target directory
        public FileManagerResponse TransferItems(string path, string targetPath, string[] names, string[] replacedItemNames, bool isCutRequest, FileManagerDirectoryContent TargetData, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse moveResponse = new FileManagerResponse();
            FileManagerDirectoryContent cwd = new FileManagerDirectoryContent();
            List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
            List<FileManagerDirectoryContent> otherFiles = new List<FileManagerDirectoryContent>();
            List<string> existFiles = new List<string>();
            try
            {
                GetBucketList();
                AccessPermission PathPermission = GetPathPermission(data[0].FilterPath + data[0].Name, false);
                if(isCutRequest) { 
                    if (PathPermission != null && (!PathPermission.Read || !PathPermission.Write))
                    {
                        accessMessage = PathPermission.Message;
                        throw new UnauthorizedAccessException("'" + data[0].Name + "' is not accessible. You need permission to perform the Write action.");
                    }
                }
                else
                {
                    if (PathPermission != null && (!PathPermission.Read || !PathPermission.Copy))
                    {
                        accessMessage = PathPermission.Message;
                        throw new UnauthorizedAccessException("'" + data[0].Name + "' is not accessible. You need permission to perform the copy action.");
                    }
                }
                FileManagerResponse readResponse = new FileManagerResponse();
                if (targetPath == "/") ListingObjectsAsync("/", RootName, false).Wait(); else ListingObjectsAsync("/", this.RootName.Replace("/", "") + targetPath, false).Wait();
                if (targetPath == "/")
                    cwd = response.S3Objects.Where(x => x.Key == RootName).Select(y => CreateDirectoryContentInstance(y.Key.ToString().Replace("/", ""), true, "folder", y.Size, y.LastModified, y.LastModified, false, "")).ToArray()[0];
                else if (response.CommonPrefixes.Count > 0)
                    cwd = CreateDirectoryContentInstance(names[0].Contains("/") ? names[0].Split("/")[names[0].Split("/").Length - 2] : (path == "/" ? "Files" : path.Split("/")[path.Split("/").Length - 2]), false, "Folder", 0, DateTime.Now, DateTime.Now, (response.CommonPrefixes.Count > 0) ? true : false, TargetData.FilterPath);
                GetBucketList();
                if (names[0].Contains("/"))
                {
                    foreach (string name in names)
                    {
                        path = "/" + name.Substring(0, name.Length - name.Split("/")[name.Split("/").Length - (name.EndsWith("/") ? 0 : 1)].Length);
                        string n = "";
                        n = name.EndsWith("/") ? name.Split("/")[name.Split("/").Length - 2] : name.Split("/").Last();
                        if (path == "/") ListingObjectsAsync("/", RootName , false).Wait(); else ListingObjectsAsync("/", this.RootName.Replace("/", "") + path, false).Wait();
                        if (response.CommonPrefixes.Count > 0)
                        {
                            foreach (string commonPrefix in response.CommonPrefixes)
                            {
                                if (commonPrefix == this.RootName + name + "/")
                                {
                                    bool hasChild = data[response.CommonPrefixes.IndexOf(commonPrefix)].HasChild;
                                    files.Add(CreateDirectoryContentInstance(commonPrefix, false, "Folder", 0, DateTime.Now, DateTime.Now, hasChild, (TargetData.FilterPath + TargetData.Name + "/")));
                                }
                            }
                        }
                        if (response.S3Objects.Count > 0)
                        {
                            foreach (S3Object S3Object in response.S3Objects)
                            {
                                if (S3Object.Key == this.RootName.Replace("/", "") + path + n)
                                    files.Add(CreateDirectoryContentInstance(S3Object.Key, true, Path.GetExtension(S3Object.Key), S3Object.Size, S3Object.LastModified, S3Object.LastModified, false, (TargetData.FilterPath + TargetData.Name + "/")));
                            }
                        }
                    }
                }
                else
                {
                    if (path == "/") ListingObjectsAsync("/", RootName , false).Wait(); else ListingObjectsAsync("/", this.RootName.Replace("/", "") + path, false).Wait();
                    if (response.CommonPrefixes.Count > 0)
                    {
                        foreach (string commonPrefix in response.CommonPrefixes)
                        {
                            foreach (string n in names)
                            {
                                if (commonPrefix == this.RootName.Replace("/", "") + path + n + "/")
                                {
                                    bool hasChild = data[Array.IndexOf(names, n)].HasChild;
                                    files.Add(CreateDirectoryContentInstance(commonPrefix.Split("/")[commonPrefix.Split("/").Length - 2], false, "Folder", 0, DateTime.Now, DateTime.Now, hasChild, (TargetData.FilterPath + TargetData.Name + "/")));
                                }
                            }
                        }
                    }
                    if (response.S3Objects.Count > 1)
                    {
                        foreach (S3Object S3Object in response.S3Objects)
                        {
                            foreach (string n in names)
                            {
                                if (S3Object.Key == this.RootName.Replace("/", "") + path + n)
                                {
                                    files.Add(CreateDirectoryContentInstance(S3Object.Key.Split("/").Last(), true, Path.GetExtension(S3Object.Key), S3Object.Size, S3Object.LastModified, S3Object.LastModified, false, (TargetData.FilterPath + TargetData.Name + "/")));
                                }
                            }
                        }
                    }
                }
                foreach (FileManagerDirectoryContent file in files)
                {
                    if (file.Type == "Folder")
                    {
                        int directoryCount = 0;
                        string fName = (names[0].Contains("/")) ? file.Name.Split("/")[file.Name.Split("/").Length - 2] : file.Name;
                        while (this.checkFileExist(targetPath, fName + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : ""))) { directoryCount++; }
                        if (directoryCount > 0) existFiles.Add(file.Name); else otherFiles.Add(file);
                        file.Name = file.Name + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : "");
                    }
                    else
                    {
                        string fileName = file.Name.Substring(0, file.Name.Length - file.Type.Length);
                        int directoryCount = 0;
                        string fName = (names[0].Contains("/")) ? file.Name.Split("/").Last() : file.Name;
                        while (this.checkFileExist(targetPath, fName.Substring(0, fName.Length - file.Type.Length) + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : "") + file.Type)) { directoryCount++; }
                        if (directoryCount > 0) existFiles.Add(file.Name); else otherFiles.Add(file);
                        file.Name = fileName + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : "") + file.Type;
                    }
                }
            }
            catch (Exception ex) {
                ErrorDetails error = new ErrorDetails();
                error.Message = ex.Message.ToString();
                error.Code = error.Message.Contains("is not accessible. You need permission") ? "401" : "404";
                if ((error.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { error.Message = accessMessage; }
                error.FileExists = moveResponse.Error?.FileExists;
                moveResponse.Error = error;
                return moveResponse;
            }
            if (names[0].Contains("/"))
            {
                foreach (var x in names.Select((name, index) => new { name, index }))
                {
                    string nameValue = "";
                    string checkRoot = x.name;
                    path = "/" + x.name.Substring(0, x.name.Length - x.name.Split("/")[x.name.Split("/").Length - (x.name.EndsWith("/") ? 0 : 1)].Length);
                    string n = x.name.Split("/")[x.name.Split("/").Length - (x.name.EndsWith("/") ? 0 : 1)];
                    if (Path.GetExtension(x.name) == "Folder")
                    {
                        int directoryCount = 0;
                        while (this.checkFileExist(targetPath, n + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : ""))) { directoryCount++; }
                        nameValue = n + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : "");
                    }
                    else
                    {
                        string fileName = n.Substring(0, n.Length - Path.GetExtension(x.name).Length);
                        int directoryCount = 0;
                        while (this.checkFileExist(targetPath, fileName + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : "") + Path.GetExtension(x.name))) { directoryCount++; }
                        nameValue = fileName + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : "") + Path.GetExtension(x.name);
                    }
                    if (existFiles.Count == 0) { MoveDirectoryAsync((RootName + checkRoot + "/"), (RootName.Replace("/", "") + targetPath + nameValue + "/"), Path.GetExtension(x.name) != "Folder", isCutRequest); }
                    else if (replacedItemNames.Length != 0)
                    {
                        foreach (string exFile in existFiles)
                        {
                            if (x.name != exFile || replacedItemNames.Length > 0)
                                MoveDirectoryAsync((RootName + checkRoot + "/"), (RootName.Replace("/", "") + targetPath + nameValue + "/"), Path.GetExtension(x.name) != "Folder", isCutRequest);
                        }
                    }
                    else
                    {
                        foreach (FileManagerDirectoryContent otherFile in otherFiles)
                        {
                            if (existFiles.Where(p => p == x.name).Select(p => p).ToArray().Length < 1)
                                MoveDirectoryAsync((RootName + checkRoot + "/"), (RootName.Replace("/", "") + targetPath + nameValue + "/"), Path.GetExtension(x.name) != "Folder", isCutRequest);
                        }
                    }
                }
            }
            else
            {
                foreach (var x in names.Select((name, index) => new { name, index }))
                {
                    string nameValue = "";
                    if (data[x.index].Type == "Folder")
                    {
                        int directoryCount = 0;
                        while (this.checkFileExist(targetPath, x.name + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : ""))) { directoryCount++; }
                        nameValue = x.name + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : "");
                    }
                    else
                    {
                        string fileName = x.name.Substring(0, x.name.Length - data[x.index].Type.Length);
                        int directoryCount = 0;
                        while (this.checkFileExist(targetPath, fileName + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : "") + data[x.index].Type)) { directoryCount++; }
                        nameValue = fileName + (directoryCount > 0 ? "(" + directoryCount.ToString() + ")" : "") + data[x.index].Type;
                    }
                    if (existFiles.Count == 0)
                        MoveDirectoryAsync((RootName.Replace("/", "") + path + x.name + "/"), (RootName.Replace("/", "") + targetPath + nameValue + "/"), data[x.index].IsFile, isCutRequest);
                    else if (replacedItemNames.Length != 0)
                    {
                        foreach (string existFile in existFiles)
                        {
                            if (x.name != existFile || replacedItemNames.Length > 0)
                                MoveDirectoryAsync((RootName.Replace("/", "") + path + x.name + "/"), (RootName.Replace("/", "") + targetPath + nameValue + "/"), data[x.index].IsFile, isCutRequest);
                        }
                    }
                    else
                    {
                        foreach (FileManagerDirectoryContent otherFile in otherFiles)
                        {
                            if (existFiles.Where(p => p == x.name).Select(p => p).ToArray().Length < 1)
                                MoveDirectoryAsync((RootName.Replace("/", "") + path + x.name + "/"), (RootName.Replace("/", "") + targetPath + nameValue + "/"), data[x.index].IsFile, isCutRequest);
                        }
                    }
                }
            }
            if (replacedItemNames.Length == 0 && existFiles.Count > 0)
            {
                ErrorDetails er = new ErrorDetails();
                er.FileExists = existFiles;
                er.Code = "400";
                er.Message = "File Already Exists";
                moveResponse.Files = otherFiles;
                moveResponse.Error = er;
                return moveResponse;
            }
            else
            {
                Task.Delay(6000).Wait();
                moveResponse.CWD = cwd;
                moveResponse.Files = files;
                return moveResponse;
            }
        }

        //Gets the details of the file(s) or folder(s)
        public FileManagerResponse Details(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse getDetailResponse = new FileManagerResponse();
            try
            {
                GetBucketList();
                int i = names.Length;
                string location = "";
                if (names.Length > 0 && names[0].Contains("/"))
                    ListingObjectsAsync("/", RootName + names[0], false).Wait();
                else
                    ListingObjectsAsync("/", RootName.Replace("/", "") + (names.Length < 1 ? path.Substring(0, path.Length - 1) : path + data[0].Name), false).Wait();
                if (data.Length == 1)
                {
                    if (names.Length == 0 || data[i - 1].Type == "Folder")
                    {
                        if (response.CommonPrefixes.Count > 0)
                            location = response.CommonPrefixes[0].Substring(0, response.CommonPrefixes[0].Length - 1);
                    }
                    else if (response.S3Objects.Count > 0)
                        location = response.S3Objects[0].Key;
                }
                string previousLocation = "";
                foreach (string name in names)
                {
                    ListingObjectsAsync("/", RootName.Replace("/", "") + path + name + ((data[i - 1].Type == "Folder") ? "/" : ""), false).Wait();
                    i--;
                    string exactName = name.IndexOf("/") > 0 ? name.Substring(name.LastIndexOf("/")) : name;
                    int indexValue = exactName == name ? response.Prefix.LastIndexOf(exactName) - 1 : response.Prefix.LastIndexOf(exactName);
                    if (previousLocation != "")
                    {   
                        if (response.Prefix.Substring(0, indexValue) != previousLocation)
                        {
                            location = "Various Folders";
                        }
                    }
                    else
                    {
                        location = previousLocation = response.Prefix.Substring(0, indexValue);
                    }
                    foreach (S3Object key in response.S3Objects) { sizeValue = sizeValue + key.Size; }
                    if (response.CommonPrefixes.Count > 0) this.getChildObjects(response.CommonPrefixes, true, "");
                }
                if (names.Length < 1) this.getChildObjects(response.CommonPrefixes, true, "");
                FileDetails detailFiles = new FileDetails();
                detailFiles = new FileDetails
                {
                    Name = data.Length == 1 ? (String.IsNullOrEmpty(data[0].Name) ? path.Split("/")[path.Split("/").Length - 2] : data[0].Name) : string.Join(", ", data.Select(x => x.Name).ToArray()),
                    IsFile = data[0].IsFile,
                    Size = byteConversion(sizeValue).ToString(),
                    Modified = data.Length == 1 && data[0].IsFile ? data[0].DateModified : DateTime.Now,
                    Created = data.Length == 1 && data[0].IsFile ? data[0].DateCreated : DateTime.Now,
                    MultipleFiles = data.Length == 1 ? false : true,
                    Location = location,
                };
                ListObjectsResponse res = response;
                getDetailResponse.Details = detailFiles;
            }
            catch (Exception ex) { throw ex; }
            return getDetailResponse;
        }

        public bool checkFileExist(string path, string name)
        {
            GetBucketList();
            ListingObjectsAsync("/", RootName.Replace("/", "") + path, false).Wait();
            bool checkExist = false;
            if (response.CommonPrefixes.Count > 0)
            {
                foreach (string commonPrefix in response.CommonPrefixes)
                {
                    if (commonPrefix.Split("/")[commonPrefix.Split("/").Length - 2].ToLower() == name.ToLower()) { checkExist = true; break; }
                }
            }
            if (response.S3Objects.Count > 0)
            {
                foreach (S3Object s3Object in response.S3Objects)
                {
                    if (s3Object.Key.ToLower() == (RootName.Replace("/", "") + path + name).ToLower()) { checkExist = true; break; }
                }
            }
            return checkExist;
        }

        // Creates a NewFolder
        public FileManagerResponse Create(string path, string name, params FileManagerDirectoryContent[] data)
        {
            FileManagerResponse createResponse = new FileManagerResponse();
            AccessPermission PathPermission = GetPathPermission(data[0].FilterPath+ data[0].Name, false);
            if (checkFileExist(path, name))
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "400";
                er.Message = "A file or folder with the name " + name + " already exists.";
                createResponse.Error = er;
                return createResponse;
            }
            else
            {
                try
                {
                    if (PathPermission != null && (!PathPermission.Read || !PathPermission.WriteContents))
                    {
                        accessMessage = PathPermission.Message;
                        throw new UnauthorizedAccessException("'" + name + "' is not accessible. You need permission to perform the writeContents action.");
                    }
                    GetBucketList();
                    FileManagerDirectoryContent CreateData = new FileManagerDirectoryContent();
                    string key = string.Format(@"{0}/", RootName.Replace("/", "") + path + name);
                    PutObjectRequest request = new PutObjectRequest() { Key = key, BucketName = bucketName };
                    request.InputStream = new MemoryStream();
                    client.PutObjectAsync(request);
                    CreateData = CreateDirectoryContentInstance(name, false, "Folder", 0, DateTime.Now, DateTime.Now, false, path);
                    FileManagerDirectoryContent[] newData = new FileManagerDirectoryContent[] { CreateData };
                    createResponse.Files = newData;
                    return createResponse;
                }
                catch (Exception ex) {
                    ErrorDetails er = new ErrorDetails();
                    er.Message = ex.Message.ToString();
                    er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                    if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                    createResponse.Error = er;
                    return createResponse;
                }
            }
        }

        // Search for file(s) or folder(s)
        public FileManagerResponse Search(string path, string searchString, bool showHiddenItems, bool caseSensitive, params FileManagerDirectoryContent[] data)
        {
            Task.Delay(2000).Wait();
            FileManagerResponse searchResponse = new FileManagerResponse();
            try
            {
                GetBucketList();
                if (path == "/") ListingObjectsAsync("/", RootName, false).Wait(); else ListingObjectsAsync("/", this.RootName.Replace("/", "") + path, false).Wait();
                List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
                List<FileManagerDirectoryContent> filesS3 = new List<FileManagerDirectoryContent>();
                char[] j = new Char[] { '*' };
                if (response.CommonPrefixes.Count > 0)
                    files = response.CommonPrefixes.Where(x => x.Split("/")[x.Split("/").Length - 2].ToLower().Contains(searchString.TrimStart(j).TrimEnd(j).ToLower())).Select(x => CreateDirectoryContentInstance(x.Split("/")[x.Split("/").Length - 2], false, "Folder", 0, DateTime.Now, DateTime.Now, this.checkChild(x), x.Substring(0, x.Length - x.Split("/")[x.Split("/").Length - 2].Length - 1).Substring(RootName.Length - 1))).ToList();
                if (response.S3Objects.Count > 1)
                { // Ensure HasChild property
                    filesS3 = response.S3Objects.Where(x => (x.Key != RootName && x.Key.Split("/")[x.Key.Split("/").Length - 1].ToLower().Contains(searchString.TrimStart(j).TrimEnd(j).ToLower()))).Select(y =>
                    CreateDirectoryContentInstance(y.Key.Split("/").Last(), true, Path.GetExtension(y.Key.ToString()), y.Size, y.LastModified, y.LastModified, false, y.Key.Substring(0, y.Key.Length - y.Key.Split("/")[y.Key.Split("/").Length - 1].Length).Substring(RootName.Length - 1))).ToList();
                }
                if (response.CommonPrefixes.Count > 0) getChildObjects(response.CommonPrefixes, false, searchString);
                if (filesS3.Count != 0) files = files.Union(filesS3).ToList();
                if (s3ObjectFiles.Count != 0) files = files.Union(s3ObjectFiles).ToList();
                searchResponse.Files = files;
            }
            catch (Exception ex) { throw ex; }
            searchResponse.CWD = data[0];
            return searchResponse;
        }

        // Renames a file or folder
        public FileManagerResponse Rename(string path, string name, string newName, bool replace = false, bool showFileExtension = true, params FileManagerDirectoryContent[] data)
        {
            return AsyncRename(path, name, newName, replace, showFileExtension, data).Result;
        }
        public virtual async Task<FileManagerResponse> AsyncRename(string path, string name, string newName, bool replace, bool showFileExtension, params FileManagerDirectoryContent[] data)
        {
            GetBucketList();
            FileManagerResponse renameResponse = new FileManagerResponse();
            FileManagerDirectoryContent cwd = new FileManagerDirectoryContent();
            AccessPermission PathPermission = GetPathPermission(data[0].FilterPath + data[0].Name, data[0].IsFile);
            List<FileManagerDirectoryContent> files = new List<FileManagerDirectoryContent>();
            if (checkFileExist(data[0].FilterPath, newName))
            {
                ErrorDetails er = new ErrorDetails();
                er.Code = "400";
                er.Message = "Cannot rename " + name + " to " + newName + ": destination already exists.";
                renameResponse.Error = er;
                return renameResponse;
            }
            else
            {
                await MoveDirectoryAsync((RootName.Replace("/", "") + data[0].FilterPath + name + "/"), (RootName.Replace("/", "") + data[0].FilterPath + newName + "/"), data[0].IsFile, true);
                try
                {
                    if (PathPermission != null && (!PathPermission.Read || !PathPermission.Write))
                    {
                        accessMessage = PathPermission.Message;
                        throw new UnauthorizedAccessException();
                    }
                    GetBucketList();
                    FileManagerResponse readResponse = new FileManagerResponse();
                    if (path == "/") ListingObjectsAsync("/", RootName , false).Wait(); else ListingObjectsAsync("/", this.RootName.Replace("/", "") + path, false).Wait();
                    if (path == "/")
                        cwd = response.S3Objects.Where(x => x.Key == RootName).Select(y => CreateDirectoryContentInstance(y.Key.ToString().Replace("/", ""), true, "folder", y.Size, y.LastModified, y.LastModified, false, data[0].FilterPath)).ToArray()[0];
                    else if (response.CommonPrefixes.Count > 0)
                        cwd = CreateDirectoryContentInstance(path.Split("/")[path.Split("/").Length - 2], false, "Folder", 0, DateTime.Now, DateTime.Now, (response.CommonPrefixes.Count > 0) ? true : false, "");
                    GetBucketList();
                    if (data[0].FilterPath == "/") ListingObjectsAsync("/", RootName, false).Wait(); else ListingObjectsAsync("/", this.RootName.Replace("/", "") + data[0].FilterPath, false).Wait();
                    if (response.CommonPrefixes.Count > 1)
                    {
                        foreach (string commonPrefix in response.CommonPrefixes)
                        {
                            if (commonPrefix == this.RootName.Replace("/", "") + path + newName + "/")
                                files.Add(CreateDirectoryContentInstance(commonPrefix.Split("/")[commonPrefix.Split("/").Length - 2], false, "Folder", 0, DateTime.Now, DateTime.Now, false, data[0].FilterPath));
                        }
                    }
                    if (response.S3Objects.Count > 1)
                    {
                        foreach (S3Object S3Object in response.S3Objects)
                        {
                            if (S3Object.Key == this.RootName.Replace("/", "") + data[0].FilterPath + (showFileExtension ? newName : (newName + data[0].Type)))
                                files.Add(CreateDirectoryContentInstance(S3Object.Key.Split("/").Last(), true, Path.GetExtension(S3Object.Key), S3Object.Size, S3Object.LastModified, S3Object.LastModified, false, data[0].FilterPath));
                        }
                    }
                    renameResponse.Files = files;
                    return renameResponse;
                }
                catch (Exception ex) {
                    ErrorDetails er = new ErrorDetails();
                    er.Message = (ex.GetType().Name == "UnauthorizedAccessException") ? "'" + name + "' is not accessible. You need permission to perform the write action." : ex.Message.ToString();
                    er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                    if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                    renameResponse.Error = er;
                    renameResponse.Files = files;
                    return renameResponse;
                }
            }
        }

        public FileManagerResponse Upload(string path, IList<IFormFile> uploadFiles, string action, int chunkIndex, int totalChunk, FileManagerDirectoryContent[] data)
        {
            return AsyncUpload(path, uploadFiles, action, chunkIndex, totalChunk, data).Result;
        }

        // Uploads the file(s)
        public virtual async Task<FileManagerResponse> AsyncUpload(string path, IList<IFormFile> uploadFiles, string action, int chunkIndex, int totalChunk, FileManagerDirectoryContent[] data)
        {
            FileManagerResponse uploadResponse = new FileManagerResponse();
            AccessPermission PathPermission = GetPathPermission(data[0].FilterPath + data[0].Name, false);
            try
            {
                if (PathPermission != null && (!PathPermission.Read || !PathPermission.Upload))
                {
                    accessMessage = PathPermission.Message;
                    throw new UnauthorizedAccessException("'" + data[0].Name + "' is not accessible. You need permission to perform the upload action.");
                }
                string fileName = Path.GetFileName(uploadFiles[0].FileName);
                fileName = fileName.Replace("../", "");
                GetBucketList();
                List<string> existFiles = new List<string>();
                foreach (IFormFile file in uploadFiles)
                {
                    string[] folders = file.FileName.Split('/');
                    string name = folders[folders.Length - 1];
                    string fullName = Path.Combine(Path.GetTempPath(), name);
                    fullName = fullName.Replace("../", "");
                    if (uploadFiles != null)
                    {
                        bool isValidChunkUpload = file.ContentType == "application/octet-stream";
                        if (action == "save")
                        {
                            bool isExist = checkFileExist(path, name);
                            if (isExist)
                            {
                                existFiles.Add(name);
                            }
                            else
                            {
                                if (isValidChunkUpload)
                                {
                                    await PerformChunkedUpload(file, bucketName, chunkIndex, totalChunk, RootName.Replace("/", "") + path + fileName);
                                }
                                else
                                {
                                    await PerformDefaultUpload(file, fileName, path);
                                }
                            }
                        }
                        else if (action == "replace")
                        {
                            if (System.IO.File.Exists(fullName))
                            {
                                System.IO.File.Delete(fullName);
                            }
                            if (isValidChunkUpload)
                            {
                                await PerformChunkedUpload(file, bucketName, chunkIndex, totalChunk, RootName.Replace("/", "") + path + fileName);
                            }
                            else
                            {
                                await PerformDefaultUpload(file, fileName, path);
                            }
                        }
                        else if (action == "keepboth")
                        {
                            string newName = fullName;
                            string newFileName = file.FileName;
                            int index = fullName.LastIndexOf(".");
                            int indexValue = newFileName.LastIndexOf(".");
                            if (index >= 0)
                            {
                                newName = fullName.Substring(0, index);
                                newFileName = newFileName.Substring(0, indexValue);
                            }
                            int fileCount = 0;
                            while (checkFileExist(path, newFileName + (fileCount > 0 ? "(" + fileCount.ToString() + ")" + Path.GetExtension(name) : Path.GetExtension(name))))
                            {
                                fileCount++;
                            }
                            newName = newFileName + (fileCount > 0 ? "(" + fileCount.ToString() + ")" : "") + Path.GetExtension(name);
                            GetBucketList();
                            if (isValidChunkUpload)
                            {
                                await PerformChunkedUpload(file, bucketName, chunkIndex, totalChunk, RootName.Replace("/", "") + path + newName);
                            }
                            else
                            {
                                await PerformDefaultUpload(file, newName, path);
                            }
                        }
                        else if (action == "remove")
                        {
                            if (System.IO.File.Exists(fullName))
                            {
                                System.IO.File.Delete(fullName);
                            }
                            else
                            {
                                ErrorDetails er = new ErrorDetails();
                                er.Code = "404";
                                er.Message = "File not found.";
                                uploadResponse.Error = er;
                            }
                        }
                    }
                }
                if (existFiles.Count != 0)
                {
                    ErrorDetails er = new ErrorDetails();
                    er.FileExists = existFiles;
                    er.Code = "400";
                    er.Message = "File Already Exists";
                    uploadResponse.Error = er;
                }
                return uploadResponse;
            }
            catch (Exception ex) {
                ErrorDetails er = new ErrorDetails();
                er.Message = (ex.GetType().Name == "UnauthorizedAccessException") ? "'" + data[0].Name + "' is not accessible. You need permission to perform the upload action." : ex.Message.ToString();
                er.Code = er.Message.Contains("is not accessible. You need permission") ? "401" : "417";
                if ((er.Code == "401") && !string.IsNullOrEmpty(accessMessage)) { er.Message = accessMessage; }
                uploadResponse.Error = er;
                return uploadResponse;
            }
        }

        private async Task PerformDefaultUpload(IFormFile file, string fileName, string path)
        {
            using (var stream = file.OpenReadStream())
            {
                await fileTransferUtility.UploadAsync(stream, bucketName, RootName.Replace("/", "") + path + fileName);
            }
        }

        public async Task PerformChunkedUpload(IFormFile file, string bucketName, int chunkIndex, int totalChunk, string keyName)
        {
            try
            {
                if (chunkIndex == 0)
                {
                    uploadId = string.Empty;
                    partETags = new List<PartETag>();
                    var initiateRequest = new InitiateMultipartUploadRequest
                    {
                        BucketName = bucketName,
                        Key = keyName
                    };
                    var initResponse = await client.InitiateMultipartUploadAsync(initiateRequest);
                    uploadId = initResponse.UploadId;
                }

                using (var stream = file.OpenReadStream())
                {
                    var uploadPartRequest = new UploadPartRequest
                    {
                        BucketName = bucketName,
                        Key = keyName,
                        UploadId = uploadId,
                        PartNumber = chunkIndex + 1,
                        InputStream = stream,
                        PartSize = stream.Length
                    };

                    var uploadPartResponse = await client.UploadPartAsync(uploadPartRequest);
                    partETags.Add(new PartETag(uploadPartResponse.PartNumber, uploadPartResponse.ETag));
                    if (chunkIndex == totalChunk - 1)
                    {
                        var completeRequest = new CompleteMultipartUploadRequest
                        {
                            BucketName = bucketName,
                            Key = keyName,
                            UploadId = uploadId,
                            PartETags = partETags
                        };
                        await client.CompleteMultipartUploadAsync(completeRequest);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Returns the image
        public virtual FileStreamResult GetImage(string path, string id, bool allowCompress, ImageSize size, params FileManagerDirectoryContent[] data)
        {
            try
            {
                AccessPermission PathPermission = GetPathPermission(path, false);
                if (PathPermission != null && !PathPermission.Read)
                {
                    return null;
                }
                GetBucketList();
                ListingObjectsAsync("/", RootName.Replace("/", "") + path, false).Wait();
                string fileName = path.ToString().Split("/").Last();
                fileName = fileName.Replace("../", "");
                Stream stream = fileTransferUtility.OpenStream(bucketName, RootName.Replace("/", "") + path);
                return new FileStreamResult(stream, "APPLICATION/octet-stream");
            }
            catch (Exception ex) { throw ex; }
        }

        // Download file(s) or folder(s)
        public virtual FileStreamResult Download(string path, string[] Names, params FileManagerDirectoryContent[] data)
        {
            return DownloadAsync(path, Names, data).GetAwaiter().GetResult();
        }

        public virtual async Task<FileStreamResult> DownloadAsync(string path, string[] names, params FileManagerDirectoryContent[] data)
        {
            GetBucketList();
            FileStreamResult fileStreamResult = null;

            if (names.Length == 1)
            {
                GetBucketList();
                await ListingObjectsAsync("/", RootName.Replace("/", "") + path + names[0], false);
            }

            if (names.Length == 1 && response.CommonPrefixes.Count == 0)
            {
                try
                {
                    AccessPermission pathPermission = GetPathPermission(path + names[0], true);
                    if (pathPermission != null && (!pathPermission.Read || !pathPermission.Download))
                    {
                        throw new UnauthorizedAccessException("'" + names[0] + "' is not accessible. Access is denied.");
                    }

                    GetBucketList();
                    await ListingObjectsAsync("/", RootName.Replace("/", "") + path, false);

                    Stream stream = await fileTransferUtility.OpenStreamAsync(bucketName, RootName.Replace("/", "") + path + names[0]);

                    fileStreamResult = new FileStreamResult(stream, "APPLICATION/octet-stream");
                    fileStreamResult.FileDownloadName = names[0].Contains("/") ? names[0].Split("/").Last() : names[0];

                    return fileStreamResult;
                }
                catch (AmazonS3Exception amazonS3Exception)
                {
                    throw amazonS3Exception;
                }
            }
            else
            {
                try
                {
                    var memoryStream = new MemoryStream();

                    using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                    {
                        foreach (string folderName in names)
                        {
                            AccessPermission pathPermission = GetPathPermission(path + folderName, Path.GetExtension(folderName) == "" ? false : true);
                            if (pathPermission != null && (!pathPermission.Read || !pathPermission.Download))
                            {
                                throw new UnauthorizedAccessException("'" + folderName + "' is not accessible. Access is denied.");
                            }

                            var initialResponse = await GetRecursiveResponse("/", RootName.Replace("/", "") + path + folderName, false);
                            await DownloadSubdirectories(archive, folderName, path + folderName, RootName.Replace("/", "") + path + folderName, initialResponse);
                        }
                    }

                    memoryStream.Seek(0, SeekOrigin.Begin);

                    fileStreamResult = new FileStreamResult(memoryStream, "APPLICATION/octet-stream");
                    fileStreamResult.FileDownloadName = "Files.zip";

                    return fileStreamResult;
                }
                catch (AmazonS3Exception amazonS3Exception)
                {
                    throw amazonS3Exception;
                }
            }
        }

        private async Task DownloadSubdirectories(ZipArchive archive, string folderName, string folderPath, string s3FolderPath, ListObjectsResponse response)
        {
            foreach (var item in response.S3Objects)
            {
                string filePath = item.Key.Substring(item.Key.IndexOf(folderName));
                string s3FilePath = s3FolderPath;

                Stream fileStream = await fileTransferUtility.OpenStreamAsync(bucketName, s3FilePath);
                var entry = archive.CreateEntry(filePath, CompressionLevel.Optimal);

                using (var entryStream = entry.Open())
                {
                    await fileStream.CopyToAsync(entryStream);
                }
            }
            foreach (var subdirectory in response.CommonPrefixes)
            {
                string subdirectoryName = subdirectory.Replace(s3FolderPath, "");
                var subdirectoryResponse = await GetRecursiveResponse("/", s3FolderPath + subdirectoryName, false);
                await DownloadSubdirectories(archive, folderName, folderName + subdirectoryName, s3FolderPath + subdirectoryName, subdirectoryResponse);
            }
        }

        public static async Task<ListObjectsResponse> GetRecursiveResponse(string delimiter, string prefix, bool childCheck)
        {
            try
            {
                ListObjectsRequest request = new ListObjectsRequest { BucketName = bucketName, Delimiter = delimiter, Prefix = prefix };
                return await client.ListObjectsAsync(request);
            }
            catch (AmazonS3Exception amazonS3Exception) { throw amazonS3Exception; }
        }

        // Deletes a Directory
        public async Task DeleteDirectory(string path, string[] names)
        {
            try
            {
                GetBucketList();
                DeleteObjectsRequest deleteObjectsRequest = new DeleteObjectsRequest() { BucketName = bucketName };
                foreach (string name in names)
                {
                    ListObjectsRequest listObjectsRequest = new ListObjectsRequest { BucketName = bucketName, Prefix = RootName.Replace("/", "") + path + name + (String.IsNullOrEmpty(Path.GetExtension(name)) ? "/" : ""), Delimiter = String.IsNullOrEmpty(Path.GetExtension(name)) ? null : "/" };
                    ListObjectsResponse listObjectsResponse = await client.ListObjectsAsync(listObjectsRequest);
                    foreach (S3Object s3Object in listObjectsResponse.S3Objects) { deleteObjectsRequest.AddKey(s3Object.Key); }
                }
                await client.DeleteObjectsAsync(deleteObjectsRequest);
                ListingObjectsAsync("/", RootName.Replace("/", "") + path + names[0], false).Wait();
                foreach (string name in names)
                {
                    string tempfile = Path.Combine(Path.GetTempPath(), name);
                    if (System.IO.File.Exists(tempfile)) System.IO.File.Delete(tempfile); else if (Directory.Exists(tempfile)) Directory.Delete(tempfile, true);
                }
            }
            catch (AmazonS3Exception amazonS3Exception) { throw amazonS3Exception; }
        }

        //Find all keys with a prefex of sourceKey, and rename them with destinationKey for prefix
        public async Task MoveDirectoryAsync(string sourceKey, string destinationKey, bool isFile, bool deleteS3Objects)
        {
            try
            {
                DeleteObjectsRequest deleteObjectsRequest = new DeleteObjectsRequest() { BucketName = bucketName };
                ListObjectsRequest listObjectsRequest = new ListObjectsRequest { BucketName = bucketName, Prefix = !isFile ? sourceKey : sourceKey.Substring(0, sourceKey.Length - 1), Delimiter = !isFile ? null : "/" };
                do
                {
                    ListObjectsResponse listObjectsResponse = await client.ListObjectsAsync(listObjectsRequest);
                    foreach (S3Object s3Object in listObjectsResponse.S3Objects)
                    {
                        string newKey = s3Object.Key.Replace(!isFile ? sourceKey : sourceKey.Substring(0, sourceKey.Length - 1), !isFile ? destinationKey : destinationKey.Substring(0, destinationKey.Length - 1));
                        CopyObjectRequest copyObjectRequest = new CopyObjectRequest() { SourceBucket = bucketName, DestinationBucket = bucketName, SourceKey = s3Object.Key, DestinationKey = newKey };
                        CopyObjectResponse copyObectResponse = await client.CopyObjectAsync(copyObjectRequest);
                        if (deleteS3Objects) deleteObjectsRequest.AddKey(s3Object.Key);
                    }
                    if (listObjectsResponse.IsTruncated) listObjectsRequest.Marker = listObjectsResponse.NextMarker; else listObjectsRequest = null;
                } while (listObjectsRequest != null);
                await client.DeleteObjectsAsync(deleteObjectsRequest);
            }
            catch (AmazonS3Exception amazonS3Exception) { throw amazonS3Exception; }
        }

        // Gets the child  file(s) or directories details within a directory & Calculates the folder size value
        private void getChildObjects(List<string> commonPrefixes, bool isDetailsRequest, string searchString)
        {
            try
            {
                foreach (string commonPrefix in commonPrefixes)
                {
                    ListingObjectsAsync("/", commonPrefix, false).Wait();
                    char[] j = new Char[] { '*' };
                    foreach (S3Object s3Key in response.S3Objects)
                    {
                        if (isDetailsRequest)
                            sizeValue = sizeValue + s3Key.Size;
                        else if (s3Key.Key != RootName && s3Key.Key.Split("/")[s3Key.Key.Split("/").Length - 1].ToLower().Contains(searchString.TrimStart(j).TrimEnd(j).ToLower()))
                        {
                            FileManagerDirectoryContent innerFiles = CreateDirectoryContentInstance(s3Key.Key.Split("/").Last(), true, Path.GetExtension(s3Key.Key.ToString()), s3Key.Size, s3Key.LastModified, s3Key.LastModified, false, s3Key.Key.Substring(0, s3Key.Key.Length - s3Key.Key.Split("/")[s3Key.Key.Split("/").Length - 1].Length).Substring(RootName.Length - 1));
                            s3ObjectFiles.Add(innerFiles);
                        }
                    }
                    if (response.CommonPrefixes.Count > 0)
                    {
                        List<FileManagerDirectoryContent> innerFiles = response.CommonPrefixes.Where(x => x.Split("/")[x.Split("/").Length - 2].ToLower().Contains(searchString.TrimStart(j).TrimEnd(j).ToLower())).Select(x =>
                        CreateDirectoryContentInstance(x.Split("/")[x.Split("/").Length - 2], false, "Folder", 0, DateTime.Now, DateTime.Now, this.checkChild(x), x.Substring(0, x.Length - x.Split("/")[x.Split("/").Length - 2].Length - 1).Substring(RootName.Length - 1))).ToList();
                        if (innerFiles.Count > 0) s3ObjectFiles = s3ObjectFiles != null ? s3ObjectFiles.Union(innerFiles).ToList() : innerFiles;
                        this.getChildObjects(response.CommonPrefixes, isDetailsRequest, searchString);
                    }
                }
            }
            catch (Exception ex) { throw ex; }
        }

        // Converts the bytes to definite size values
        public String byteConversion(long fileSize)
        {
            try
            {
                string[] index = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
                if (fileSize == 0) return "0 " + index[0];
                int loc = Convert.ToInt32(Math.Floor(Math.Log(Math.Abs(fileSize), 1024)));
                return (Math.Sign(fileSize) * (Math.Round(Math.Abs(fileSize) / Math.Pow(1024, loc), 1))).ToString() + " " + index[loc];
            }
            catch (AmazonS3Exception amazonS3Exception) { throw amazonS3Exception; }
        }

        public string getFilterPath(string fullPath, string path)
        {
            string name = fullPath.ToString().Replace(RootName.Replace("/", "") + path, "").Replace("/", "");
            int nameIndex = fullPath.LastIndexOf(name);
            fullPath = fullPath.Substring(0, nameIndex);
            int rootIndex = fullPath.IndexOf(RootName.Substring(0, RootName.Length - 1));
            fullPath = fullPath.Substring(rootIndex + RootName.Length - 1);
            return fullPath;
        }

        public bool checkChild(string path)
        {
            try { ListingObjectsAsync("/", path, true).Wait(); } catch (AmazonS3Exception amazonS3Exception) { throw amazonS3Exception; }
            return childResponse.CommonPrefixes.Count > 0 ? true : false;
        }

        public static async Task ListingObjectsAsync(string delimiter, string prefix, bool childCheck)
        {
            try
            {
                ListObjectsRequest request = new ListObjectsRequest { BucketName = bucketName, Delimiter = delimiter, Prefix = prefix };
                if (childCheck)
                    childResponse = await client.ListObjectsAsync(request);
                else
                    response = await client.ListObjectsAsync(request);
            }
            catch (AmazonS3Exception amazonS3Exception) { throw amazonS3Exception; }
        }
        public string ToCamelCase(FileManagerResponse userData)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            return JsonSerializer.Serialize(userData, options);
        }
        protected virtual string[] GetFolderDetails(string path)
        {
            string[] str_array = path.Split('/'), fileDetails = new string[2];
            string parentPath = "";
            for (int i = 0; i < str_array.Length - 1; i++)
            {
                parentPath += str_array[i] + "/";
            }
            fileDetails[0] = parentPath;
            fileDetails[1] = str_array[str_array.Length - 1];
            return fileDetails;
        }
        protected virtual AccessPermission GetPathPermission(string path, bool isFile)
        {
            string[] fileDetails = GetFolderDetails(path);
            if (isFile)
            {
                return GetPermission(fileDetails[0].TrimStart('/')+ fileDetails[1], fileDetails[1], true);
            }
            return GetPermission(fileDetails[0].TrimStart('/') + fileDetails[1], fileDetails[1], false);

        }
        protected virtual AccessPermission GetPermission(string location, string name, bool isFile )
        {
            AccessPermission permission = new AccessPermission();
            if (!isFile)
            {
                if (this.AccessDetails.AccessRules == null) { return null; }
                foreach (AccessRule folderRule in AccessDetails.AccessRules)
                {
                    if (folderRule.Path != null && folderRule.IsFile == false && (folderRule.Role == null || folderRule.Role == AccessDetails.Role))
                    {
                        if (folderRule.Path.IndexOf("*") > -1)
                        {
                            string parentPath = folderRule.Path.Substring(0, folderRule.Path.IndexOf("*"));
                            if ((location).IndexOf((parentPath)) == 0 || parentPath == "")
                            {
                                permission = UpdateFolderRules(permission, folderRule);
                            }
                        }
                        else if ((folderRule.Path) == (location) || (folderRule.Path) == (location + Path.DirectorySeparatorChar) || (folderRule.Path) == (location + "/"))
                        {
                            permission = UpdateFolderRules(permission, folderRule);
                        }
                        else if ((location).IndexOf((folderRule.Path)) == 0)
                        {
                            permission = UpdateFolderRules(permission, folderRule);
                        }
                    }
                }
                return permission;
            }
            else
            {
                if (this.AccessDetails.AccessRules == null) return null;
                string nameExtension = Path.GetExtension(name).ToLower();
                string fileName = Path.GetFileNameWithoutExtension(name);
                //string currentPath = GetPath(location);
                string currentPath = (location +"/");
                foreach (AccessRule fileRule in AccessDetails.AccessRules)
                {
                    if (!string.IsNullOrEmpty(fileRule.Path) && fileRule.IsFile && (fileRule.Role == null || fileRule.Role == AccessDetails.Role))
                    {
                        if (fileRule.Path.IndexOf("*.*") > -1)
                        {
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf("*.*"));
                            if (currentPath.IndexOf((parentPath)) == 0 || parentPath == "")
                            {
                                permission = UpdateFileRules(permission, fileRule);
                            }
                        }
                        else if (fileRule.Path.IndexOf("*.") > -1)
                        {
                            string pathExtension = Path.GetExtension(fileRule.Path).ToLower();
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf("*."));
                            if (((parentPath) == currentPath || parentPath == "") && nameExtension == pathExtension) {
                                permission = UpdateFileRules(permission, fileRule);
                            }
                        }
                        else if (fileRule.Path.IndexOf(".*") > -1)
                        {
                            string pathName = Path.GetFileNameWithoutExtension(fileRule.Path);
                            string parentPath = fileRule.Path.Substring(0, fileRule.Path.IndexOf(pathName + ".*"));
                            if (((parentPath) == currentPath || parentPath == "") && fileName == pathName)
                            {
                                permission = UpdateFileRules(permission, fileRule);
                            }
                        }
                        else if ((fileRule.Path) == (Path.GetFileNameWithoutExtension(location)) || fileRule.Path == location || (fileRule.Path + nameExtension == location))
                        {
                            permission = UpdateFileRules(permission, fileRule);
                        }
                    }
                }
                return permission;
            }

        }
        protected virtual bool HasPermission(Permission rule)
        {
            return rule == Permission.Allow ? true : false;
        }
        protected virtual AccessPermission UpdateFolderRules(AccessPermission folderPermission, AccessRule folderRule)
        {
            folderPermission.Copy = HasPermission(folderRule.Copy);
            folderPermission.Download = HasPermission(folderRule.Download);
            folderPermission.Write = HasPermission(folderRule.Write);
            folderPermission.WriteContents = HasPermission(folderRule.WriteContents);
            folderPermission.Read = HasPermission(folderRule.Read);
            folderPermission.Upload = HasPermission(folderRule.Upload);
            folderPermission.Message = string.IsNullOrEmpty(folderRule.Message) ? string.Empty : folderRule.Message;
            return folderPermission;
        }
        protected virtual AccessPermission UpdateFileRules(AccessPermission filePermission, AccessRule fileRule)
        {
            filePermission.Copy = HasPermission(fileRule.Copy);
            filePermission.Download = HasPermission(fileRule.Download);
            filePermission.Write = HasPermission(fileRule.Write);
            filePermission.Read = HasPermission(fileRule.Read);
            filePermission.Message = string.IsNullOrEmpty(fileRule.Message) ? string.Empty : fileRule.Message;
            return filePermission;
        }
    }
}
