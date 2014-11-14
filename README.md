LDIFPowerShell
==============

PowerShell support for processing Active Directory LDIF files

This project produces a PowerShell module that supports the ability to process Active Directory LDIF files to PowerShell.

The main cmdlet is Get-LDIFRecords. It pulls LDIF records from an LDIF file and outputs them to the PowerShell pipeline where
you can perform more processing. For instance, if you want to get the first and last names of all of the users in an
LDIF file sorted by last name, you would do something like this:

Get-LDIFRecords domain.ldif | Where {$_.objectClass –eq ‘user’ } | Select givenName, sn | Sort sn

The included binary module LDIFDistinguishedName provides special functions for manipulating distinguished names. The
LDIFDistinguishedName class exposes member functions to retrieve the RDN, the name and type of the RDN, the parent 
container distinguished name, the depth of the name in the tree, as well as an array containing the parent hierarchy. 
This simplies certain problems when trying to clone an Active Directory environment to a test environment, for instance, 
creating the container hierarchy. Because the records in the LDIF file aren't guaranteed to be in breadth-first tree order, 
you run into the problem of creating containers before their parent containers exist. Using the LDIFDistinguishedName class,
you can do something like this:

Get-LDIFRecords domain.ldif |
 Where {$_.objectClass –eq ‘container’ –or $_.objectClass –eq ‘organizationalUnit’} |
 Select objectClass, dn, @{name=’depth’;expression={$_.dn.Depth}} |
 Sort depth |
 For-Each {New-ADObject -Type $($_.objectClass[-1]) -Name $($_.dn.Name) -Path $($_.dn.Parent)}
 
This will recreate the OU and container hierarchy from an LDIF file into your AD test environment.

LDIFPowerShell is a part of another project designed to automate the creation of Active Directory test environments using
only LDIF files, Hyper-V, and PowerShell. Details of the project are at http://gilkirkpatrick.com/blog.

-gil

Copyright (c) 2014 Red Giraffe, LLC
