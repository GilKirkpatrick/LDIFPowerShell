using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;

namespace RedGiraffe.Powershell.ActiveDirectory
{
    /// <summary>
    /// The NewLDIFDistinguishedName class implements the PowerShell cmdlet New-LDIFDistinguishedName. It produces a new LDIFDistinguishedName
    /// object from its string argument and places the result on the output pipeline.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "LDIFDistinguishedName")]
    public class NewLDIFDistinguishedName : PSCmdlet
    {
        private LDIFDistinguishedName _dn;

        [Parameter(
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            Position = 0,
            HelpMessage = "A string containing the distinguished name (DN)")]
        [Alias("distinguishedName,DN")]
        public string DNString
        {
            get { return _dn.DN; }
            set { _dn = new LDIFDistinguishedName(value); }
        }
        protected override void ProcessRecord()
        {
            WriteObject(_dn);
        }
    }

    /// <summary>
    /// LDIFDistinguishedName represents the dn: attribute of an LDIF record, and provides various functions on the distinguished name
    /// </summary>
    public class LDIFDistinguishedName : IComparable
    {
        private readonly string _dnString;
        private readonly List<RelativeDistinguishedName> _segments = new List<RelativeDistinguishedName>();

        /// <summary>
        /// Returns the LDIFDistinguishedName as a regular string
        /// </summary>
        public string DN
        {
            get { return _dnString; }
        }

        /// <summary>
        /// Returns the RDN portion of an LDIFDistinguishedName as a string
        /// </summary>
        public string RDN
        {
            get { return _segments.Count == 0 ? "" : String.Format("{0}={1}", _segments[0].Attr, _segments[0].Value); }
        }

        /// <summary>
        /// Returns an array of the LDIFDistinguishedNames of the container hierarchy of the DN
        /// </summary>
        public LDIFDistinguishedName[] ParentHierarchy
        {
            get
            {
                if (Depth <= 1)
                {
                    return new LDIFDistinguishedName[0];
                }

                int nEntries = Depth - 1;
                LDIFDistinguishedName[] hierarchy = new LDIFDistinguishedName[nEntries];

                LDIFDistinguishedName parentDN = Parent;
                for (int i = nEntries - 1; i >= 0; i--)
                {
                    hierarchy[i] = parentDN;
                    parentDN = parentDN.Parent;
                }
                return hierarchy;
            }
        }

        /// <summary>
        /// Returns an LDIFDistinguishedName representing the parent container of the DN
        /// </summary>
        public LDIFDistinguishedName Parent
        {
            get
            {
                StringBuilder parent = new StringBuilder();

                for (int i = 1; i < _segments.Count; i++)
                {
                    parent.AppendFormat("{0}={1}", _segments[i].Attr, _segments[i].Value);
                    if (i < _segments.Count - 1)
                    {
                        parent.Append(",");
                    }
                }
                return new LDIFDistinguishedName(parent.ToString());
            }
        }

        /// <summary>
        /// Returns the attribute type of the RDN of the distinguished name. For instance, if the DN is CN=foo,CN=bar,O=baz, this function
        /// would return "CN"
        /// </summary>
        public string NameType
        {
            get { return _segments.Count == 0 ? "" : _segments[0].Attr; }
        }

        /// <summary>
        /// Returns the name of the RDN of the distinguished name. For instance, if the DN is CN=foo,CN=bar,OI=baz, this function would return
        /// "foo"
        /// </summary>
        public string Name
        {
            get { return _segments.Count == 0 ? "" : _segments[0].Value; }
        }

        /// <summary>
        /// Returns the depth in the hierarchy of the distinguished name. An empty DN would be depth 0, "CN=foo" would be depth 1, etc.
        /// </summary>
        public int Depth
        {
            get { return _segments.Count; }
        }

        /// <summary>
        /// Copy constructor for LDIFDistinguishedName. Because the class is read-only (there are no mutators), the copy constructor
        /// assigns the _segments array by reference.
        /// </summary>
        /// <param name="dn">The LDIFDistinguishedName to copy</param>
        public LDIFDistinguishedName(LDIFDistinguishedName dn)
        {
            _dnString = dn.DN;
            _segments = dn._segments;
        }

        /// <summary>
        /// Conversion of LDIFDistinguishedName to string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _dnString;
        }

        /// <summary>
        /// Implicit conversion from string to LDIFDistinguishedName
        /// </summary>
        /// <param name="dnString">The string to convert to a LDIFDistinguishedName object</param>
        /// <returns></returns>
        public static implicit operator LDIFDistinguishedName(string dnString)
        {
            return new LDIFDistinguishedName(dnString);
        }

        /// <summary>
        /// Comparison function for LDIFDistinguishedName is defined as the string comparison of the two _dnStrings.
        /// </summary>
        /// <param name="rhs">The LDIFDistinguishedName to compare to</param>
        /// <returns></returns>
        public int CompareTo(object rhs)
        {
            if (rhs.GetType() != typeof(LDIFDistinguishedName))
            {
                throw new ArgumentException(
                    "Comparison of LDIFDistinguishedName to an object that is not an LDIFDistinguishedName.");
            }
            return _dnString.CompareTo(((LDIFDistinguishedName)rhs)._dnString);
        }

        /// <summary>
        /// Primary constructor for LDIFDistinguishedName. Parses and validates the incoming string, and stores it as a list of relative distinguished names.
        /// </summary>
        /// <param name="dnString">The string to use to create the LDIFDistinguishedName</param>
        public LDIFDistinguishedName(string dnString)
        {
            const string escapables = ",+\"<>;=\x0a\x0d";
            const string hexDigits = "0123456789abcdefABCDEF";

            _dnString = dnString;

            char[] chars = dnString.ToCharArray();
            int i = 0;
            while (i < dnString.Length) // 
            {
                StringBuilder attrType = new StringBuilder();
                StringBuilder attrValue = new StringBuilder();

                for (; i < dnString.Length && chars[i] != '='; i++)
                {
                    if (escapables.IndexOf(chars[i]) != -1)
                    // there should be no escapable characters in the attr type name
                    {
                        throw new Exception(String.Format(
                            "Unexpected character {0} in attribute type portion of DN {1}", chars[i], dnString));
                    }
                    attrType.Append(chars[i]);
                }

                i++; // skip '='

                for (; i < dnString.Length && chars[i] != ','; i++)
                {
                    if (chars[i] != '\\')
                    {
                        attrValue.Append(chars[i]);
                    }
                    else
                    {
                        // skip the backslash, check for end of string
                        i++;
                        if (i >= dnString.Length) break;

                        // what follows is either 2 hex digits or an escaped character
                        if (i + 1 < dnString.Length && hexDigits.IndexOf(chars[i]) != -1 &&
                            hexDigits.IndexOf(chars[i + 1]) != -1)
                        {
                            string s = Convert.ToString((char)Convert.ToByte(dnString.Substring(i, 2), 16));
                            attrValue.Append(s);
                            i++; // we scanned 2 characters
                        }
                        else
                        {
                            attrValue.Append(chars[i]);
                        }
                    }
                }

                _segments.Add(new RelativeDistinguishedName(attrType.ToString().Trim(), attrValue.ToString().Trim()));
                i++; // skip ,
            }
        }

        /// <summary>
        /// The Comparator class implements the IComparer interfaces needed to use with generics that need a comparison 
        /// </summary>
        public class Comparator : IComparer, IComparer<LDIFDistinguishedName>
        {
            public int Compare(object lhs, object rhs)
            {
                if (lhs.GetType() != typeof(LDIFDistinguishedName) || rhs.GetType() != typeof(LDIFDistinguishedName))
                {
                    throw new ArgumentException(
                        "Comparison of LDIFDistinguishedName to an object that is not an LDIFDistinguishedName.");
                }

                return ((LDIFDistinguishedName)lhs).CompareTo((LDIFDistinguishedName)rhs);
            }
            public int Compare(LDIFDistinguishedName lhs, LDIFDistinguishedName rhs)
            {
                return lhs.CompareTo(rhs);
            }
        }

        public class EqualityComparator : IEqualityComparer, IEqualityComparer<LDIFDistinguishedName>
        {
            public new bool Equals(object lhs, object rhs)
            {
                if (lhs.GetType() != typeof(LDIFDistinguishedName) || rhs.GetType() != typeof(LDIFDistinguishedName))
                {
                    throw new ArgumentException(
                        "Comparison of LDIFDistinguishedName to an object that is not an LDIFDistinguishedName.");
                }

                return ((LDIFDistinguishedName)lhs).CompareTo((LDIFDistinguishedName)rhs) == 0;
            }

            public bool Equals(LDIFDistinguishedName lhs, LDIFDistinguishedName rhs)
            {
                return lhs.CompareTo(rhs) == 0;
            }

            public int GetHashCode(LDIFDistinguishedName obj)
            {
                return GetHashCode();
            }

            public int GetHashCode(object obj)
            {
                return GetHashCode();
            }
        }

        private class RelativeDistinguishedName
        {
            public string Attr { get; private set; }
            public string Value { get; private set; }

            public RelativeDistinguishedName(string rdnString)
            {
                string[] segs = rdnString.Split('='); // should check for escaped =
                Attr = segs[0].Trim();
                Value = segs[1].Trim();
            }

            public RelativeDistinguishedName(string attr, string value)
            {
                Attr = attr;
                Value = value;
            }
        }
    }
}
