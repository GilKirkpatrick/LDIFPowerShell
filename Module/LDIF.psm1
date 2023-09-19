Function Get-LDIFRecords
{
<#
    .SYNOPSIS
		Parses an LDIF file and produces a set of hashes on the pipeline corresponding to the LDIF records.

    .DESCRIPTION
        Get-LDIFRecords parses an LDIF file and produces a set of hashes in the PowerShell pipeline corresponding to the
		LDIF records in the file. Each record is a PowerShell hash containing name-value pairs corresponding to the attributes of the LDIF
		record. The value of each attribute is provided as an array of strings, one array entry per attribute value. The dn: 
		(distinguished name) attribute is provided as a PSDistinguishedName object.

		If the LDIF file contained the following entry:

		dn: CN=DNS Settings,CN=DC2,CN=Servers,CN=Default-First-Site-Name,CN=Sites,CN=Configuration,DC=testforest,DC=com
		objectClass: top
		objectClass: msDNS-ServerSettings
		cn: DNS Settings
		distinguishedName: 
		CN=DNS Settings,CN=DC2,CN=Servers,CN=Default-First-Site-Name,CN=Sites,CN=Confi
		guration,DC=testforest,DC=com
		objectGUID:: mtUMoRh3+ECxi7hXk1j14g==

		the resulting hash in the pipeline would look like:

		dn                    : CN=DNS Settings,CN=DC2,CN=Servers,CN=Default-First-Site-Name,CN=Sites,CN=Configuration,DC=testforest,DC=com
		objectClass           : {top, msDNS-ServerSettings}
		cn                    : {DNS Settings}
		distinguishedName     : {CN=DNS Settings,CN=DC2,CN=Servers,CN=Default-First-Site-Name,CN=Sites,CN=Configuration,DC=testforest,DC=com}
		objectGUID            : {a10cd59a-7718-40f8-b18b-b8579358f5e2}

		Note 1: Even attributes that have only a single value are provided as arrays. 

		Note 2: The dn entry is an object of class PSDistinguishedName, and not a string. The distinguishedName attribute however is an array 
		containing a single string.

		Note 3: The objectGUID entry is an object of class System.Guid

    .PARAMETER InputFile
		The name of the input LDIF file to process.

	.PARAMETER AsScalar
		Because Get-LDIFRecords does not know apriori whether an attribute is single or multi-valued, it provides each attribute as an array 
		of strings. This can be inconvenient in some cases where you know the attribute is defined in the schema as single valued. The AsScalar
		parameter is an array of attribute names that Get-LDIFRecords will process as single valued. If an attribute specified by AsScalar
		actually has multiple values in the LDIF file, Get-LDIFRecords will provide only the last value encountered in the LDIF file.

    .EXAMPLE
        C:\PS>Get-LDIFRecords -InputFile c:\temp\foo.ldif
         
        This example will parse the LDIF file c:\temp\foo.ldif and produce a set of records in the PowerShell pipeline corresponding to the
		LDIF records in the file.
		
    .EXAMPLE
        C:\PS>Get-LDIFRecords -InputFile c:\temp\foo.ldif | Where {$_.objectClass -eq 'user'}

		This command will parse the enire foo.ldif file and pass the resulting LDIF records to the Where statement, which will select those
		records with an objectClass attribute containing 'user'.

    .EXAMPLE
        C:\PS>Get-LDIFRecords -InputFile c:\temp\foo.ldif -ObjectGuidAsBase64

        This example will parse the LDIF file c:\temp\foo.ldif and produce a set of records in the PowerShell pipeline corresponding to the
		LDIF records in the file. The objectGuid attribute will appear as an array containing a single Base-64 encoded string as provided in
		the LDIF file.

    .NOTES
        NAME......:  Get-LDIFRecords
        AUTHOR....:  Gil Kirkpatrick
        CREATED...:  12 Nov 2014
    .LINK
        http://www.gilkirkpatrick.com/blog
#>
	[CmdletBinding()]
	param(
		[parameter(mandatory=$true, position=1)][string]$InputFile,
		[parameter(mandatory=$false)][string[]]$AsScalar
	)

	$Script:lineNo = 0
	$path = (Get-Item -LiteralPath $InputFile).FullName
	$file = New-Object System.IO.StreamReader -Arg $path
	if(!$file){
		throw "Can't open $InputFile"
	}

	do	{
		$obj = Read-LDIFObject($file)
		write-output $obj
	
	} while (!$file.EndOfStream)

	$file.close()
}

Function Read-LDIFObject
{
	param([System.IO.StreamReader]$file)
	$attrs = @{}
	$line = $file.ReadLine()

	while($line) {
		$Script:lineNo++
		Write-Verbose "$Script:lineNo : $line"
	
		if($line -cmatch '^([^:]+):(?<colon>:*) (.*)$') { # beginning of new value?
			# $matches[1] has the attribute name
			# $matches[2] has the (possibly first part of the) value
			# $matches["colon"] has ":" indicating the value is base-64 encoded

			if($attrValue) {
				if($base64){
					if($attrName -eq "objectGuid") {
						if($ObjectGuidAsBase64 -eq $true) {
							$v = $attrValue.ToString()
						}
						else {
							$b = [System.Convert]::FromBase64String($attrValue.ToString())
						    $v = new-object -TypeName System.Guid -ArgumentList (,$b);
						}
					}
					else {
						$v = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($attrValue.ToString()))
					}
				}
				else {
					$v = $attrValue.ToString() 
				}
				if($AsScalar -contains $attrName) {
					$attr = $v;
				}
				else {
					$attr = $attr + $v # add value we just completed to attr
				}
			}

			if($matches["colon"] -eq ":") {
				$base64 = $true
			}
			else {
				$base64 = $false
			}

			if($matches[1] -ne $attrName) { # beginning of new attribute?
				if($attrName) {
					# Special case for dn attribute. Instead of an array of strings, we construct a LDIFDistinguishedName object
					if($attrName -eq "dn"){
						$temp = New-LDIFDistinguishedName $attr[0]
						[void]$attrs.Add($attrName, $temp)
					}
					else {
						[void]$attrs.Add($attrName, $attr)
					}
				}
				$attrName = $matches[1]
				$attr = @()
			}
			$attrValue = new-object Text.StringBuilder($matches[2])
		}
		elseif($line -cmatch '^ (.*)$') { # continuation of existing value?
			[void]$attrValue.Append($matches[1])
		}
		$line = $file.ReadLine()
	}
	
	# Finish up the last attribute and value after reading the blank end-of-object line
	# This is a big chunk of duplicated code
	if($attrValue) {
		if($base64){
			# Not entirely sure this is a good idea. Convert the base-64 encoded objectGuid to a System.Guid object
			if($attrName -eq "objectGuid") {
				$b = [System.Convert]::FromBase64String($attrValue.ToString())
				$v = new-object -TypeName System.Guid -ArgumentList (,$b);
			}
			else {
				$v = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($attrValue.ToString()))
			}
		}
		else {
			$v = $attrValue.ToString() 
		}
		if($AsScalar -contains $attrName) {
			$attr = $v;
		}
		else {
			$attr = $attr + $v # add value we just completed to attr
		}
	}
	
	if($attrName) {
		# Special case for dn attribute. Instead of an array of strings, we construct a PSDistinguishedName object
		if($attrName -eq "dn"){
			$temp = new-dn $attr[0]
			[void]$attrs.Add($attrName, $temp)
		}
		else {
			[void]$attrs.Add($attrName, $attr)
		}
	}
	
	return new-object -TypeName PSObject -prop $attrs
}

export-modulemember -function Get-LDIFRecords
