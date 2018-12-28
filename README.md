# BFU - Background File Uploader

BFU is a [.NET Core](https://dotnet.microsoft.com/download) 2.1 project created out of the need of combining local code editing with the necessity of immediate deployment of the changes to some remote locations and the convenience of local (optional incremental) copies. Being a Core application it is command line based and configured with a json file (there are plans for a Windows GUI version however) but compatible with almost all operating systems (see the link above). BFU currently supports SCP and FTP.

## Requirements

If your developer machine doesn't already support .NET core you must download and install the corresponding version from the link above. Windows 10 should have all you need in this respect so all my examples will go with Mac (64bit), so you should go now with [Download .NET Core SDK](https://dotnet.microsoft.com/download/thank-you/dotnet-sdk-2.2.101-macos-x64-installer). 

## First steps

 - Clone or download this project from GitHub to, let's say, ~/BFU/
 -  `cd ~/BFU/`
 - `dotnet build -c Release`
 - `dotnet BFU/bin/Release/netcoreapp2.1/BFU.dll`

Now you have the binary version of the application, you can move it or create some symbolic links for easier use.
Executing BFU without arguments will generate an example configuration file in the current directory (as stated in the output too) and exit the program. This json file will be explained below.

## The configuration file

The generated configuration file contains optional fields too, the following example is cleaned up a bit. 
> Beware that all **backslash** characters must be doubled! 

To cover various folder structures this example is from a Windows development machine.

```json
{
  "AllowMultiThreadedUpload": true,
  "TargetList": [
    {
      "Method": "Scp",
      "Username": "devel",
      "Password": "devel-pass",
      "Host": "ssh.server.com",
      "Port": 22,
      "TargetPath": "/usr/local/sf/",
      "CreateTimestampedCopies": false
    },
    {
      "Method": "Ftp",
      "Username": "designer",
      "Password": "mypass",
      "Host": "ftp.server.com",
      "Port": 21,
      "TargetPath": "/home/www/html/"
    },
    {
      "Method": "Copy",
      "TargetPath": "C:\\Users\\me\\Documents\\Backup\\",
      "CreateTimestampedCopies": true,
    }
  ],
  "IgnorePatterns": {
    "Patterns": [
      {
        "PatternType": "Directory",
        "Regex": {
          "Pattern": "[/\\\\]\\.\\w+[/\\\\]",
          "Options": 0
        }
      },
      {
        "PatternType": "File",
        "Regex": {
          "Pattern": "^\\.",
          "Options": 0
        }
      }
    ]
  },
  "LocalPath": "C:\\Users\\me\\Documents\\Development\\",
  "LogPath": "C:\\Users\\me\\Documents\\example_settings.log",
  "ChangeListPath": "C:\\Users\\me\\Documents\\example_settings.lst"
}
```

#### AllowMultiThreadedUpload

BFU allows multiple simultaneous targets, this key states whether the SCP and/or FTP transfers should go in the same time or one by one.

#### TargetList
This is the most important part of the configuration file, must contain at least one target. Every  target location is defined here by the following keys:
- Method: one of `Scp`, `Ftp` or `Copy`
- Username, Password, Host and Port are obvious informations needed by SCP or FTP connections (default ports can be skipped)
- TargetPath: the root location of the common directory structure
- CreateTimestampedCopies: if false the file will be copied and overwritten on the target location, if true every copy will have the current time appended to the filename (ex. index.html_20181227154937)

#### IgnorePatterns

This is a list of regular expressions defining file patterns to be ignored. The list can be empty, the default from our example will ignore any subdirectory or file with a name starting with a dot (ex. .git)

#### LocalPath

This mandatory item represents the root of the local directory structure. All files created or modified in this folder and all its subfolders will be uploaded (taking the ignore patterns in consideration).

#### LogPath

All operations will be logged in this file.

#### ChangeListPath

This optional file will contain a list of all uploaded files. Every file appears only once, and the only way to reset the list is to delete or empty it manually.

## Usage

After your specific configuration file (ex. my.json) is ready you should start BFU:

`dotnet BFU/bin/Release/netcoreapp2.1/BFU.dll my.json`

and go on with your development tasks.
