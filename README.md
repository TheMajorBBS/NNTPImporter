# NNTPImporter

Allows for the downloading of newsgroups from an NNTP server and then importing them into The Major BBS.

**See Releases for latest compiled version containing sample config file**

## Requirements
- MBBS running the NNTP Server and Client modules
- Access to an external NNTP Server to collect the newgroups
- MBBS forums correctly setup echoing the newsgroups being downloaded
- NNTPImporter.cfg containing a list of newsgroups to download
- It is recommended the EXE and CFG file are placed in your BBS directory

## NNTPImporter.cfg
This file contains the newsgroups you wish to download, in the form of a group name followed by the last article downloaded.  When adding a new group, set this to 0 to download all articles.

Example file:  
comp.lang.python 0  
comp.lang.c 0  

The application will update this file as it downloads new articles and does not need to be modified unless adding a new group

## Usage
NNTPImporter [MBBS server] [import directory path] [interval in minutes] [NNTP server] [username] [password] [downloadpath] [interval in minutes]

[MBBS server] = IP/hostname of your MBBS Server  
[import directory path] = location of downloaded newsgroup articles  
[interval in minutes] = how often to run the importer task (default=5)  
[NNTP server] = IP/hostname of your NNTP Server  
[username] = Username if required by NNTP server, else type nil  
[password] = Password if required by NNTP server, else type nil  
[downloadpath] = locate to download newsgroup articles  
[interval in minutes] = how often to run the downloader task (default=5)  

**Example (NNTP server requires authentication)**  
NNTPImporter 192.168.1.99 nntp_downloads 15 news.eternal-september.org myusername mypassword nntp_downloads 10
  
**Example (NNTP does not require authentication)**  
NNTPImporter 192.168.1.99 nntp_downloads 15 news.another-provider.com nil nil nntp_downloads 10 
  
Both examples above will run the importer every 15 minutes and the downloader every 10 minutes.  
Paths are relative unless specified.
