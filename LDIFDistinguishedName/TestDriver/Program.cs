using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RedGiraffe.Powershell.ActiveDirectory;


namespace RedGiraffe.Powershell.ActiveDirectory.Test
{
    class Program
    {
        private class Test
        {
            public string DN { get; set; }
            public string ExpectedRDN { get; set; }
            public LDIFDistinguishedName ExpectedParent { get; set; }
            public string ExpectedName { get; set; }
            public string ExpectedNameType { get; set; }
            public int ExpectedDepth { get; set; }
            public LDIFDistinguishedName[] ExpectedParentHierarchy { get; set; }
            public System.Type ExpectedException { get; set; }

            public Test(string dn, string rdn, LDIFDistinguishedName parent, string name, string nameType, int depth, LDIFDistinguishedName[] hierarchy, Type exceptionType = null)
            {
                DN = dn;
                ExpectedRDN = rdn;
                ExpectedParent = parent;
                ExpectedName = name;
                ExpectedNameType = nameType;
                ExpectedDepth = depth;
                ExpectedParentHierarchy = hierarchy;
                ExpectedException = exceptionType;
            }
        }

        static Test[] Tests = new Test[]
        {
            new Test("", "", new LDIFDistinguishedName(""), "", "", 0, new LDIFDistinguishedName[]{}),
            new Test("cn=foo,dc=bar", "cn=foo", "dc=bar", "foo", "cn", 2, new LDIFDistinguishedName[]{"dc=bar"}),
            new Test("cn = foo , dc=bar", "cn=foo", "dc=bar", "foo", "cn", 2, new LDIFDistinguishedName[]{"dc=bar"}),
            new Test("cn=foo\\,,dc=bar", "cn=foo,", "dc=bar", "foo,", "cn", 2, new LDIFDistinguishedName[]{"dc=bar"}),
            new Test("cn=foo\\0dbar,dc=bar", "cn=foo\rbar", "dc=bar", "foo\rbar", "cn", 2, new LDIFDistinguishedName[]{"dc=bar"}),
            new Test("cn,=foo,dc=bar", "", "", "", "", 0, new LDIFDistinguishedName[]{}, typeof(System.Exception)),
            new Test("c,n=foo,dc=bar", "", "", "", "", 0, new LDIFDistinguishedName[]{}, typeof(System.Exception)),
            new Test("CN=Sam Baxter,OU=Staging,OU=FIM,OU=LH Users,DC=lifehouserpa,DC=org,DC=au", "CN=Sam Baxter", "OU=Staging,OU=FIM,OU=LH Users,DC=lifehouserpa,DC=org,DC=au", "Sam Baxter", "CN", 7, new LDIFDistinguishedName[]
                {
                    "DC=au",
                    "DC=org,DC=au",
                    "DC=lifehouserpa,DC=org,DC=au",
                    "OU=LH Users,DC=lifehouserpa,DC=org,DC=au",
                    "OU=FIM,OU=LH Users,DC=lifehouserpa,DC=org,DC=au",
                    "OU=Staging,OU=FIM,OU=LH Users,DC=lifehouserpa,DC=org,DC=au"
                })
        };

        static void Main(string[] args)
        {
            LDIFDistinguishedName dn1 = "cn=foo,cn=bar";
            LDIFDistinguishedName dn2 = dn1;

            foreach(Test t in Tests)
            {
                Console.WriteLine("Testing [{0}]", t.DN);
                try
                {
                    LDIFDistinguishedName dn = new LDIFDistinguishedName(t.DN);
                    if(dn.RDN.CompareTo(t.ExpectedRDN) != 0)
                    {
                        Console.WriteLine("Failed: DN [{0}] RDN {1} expected {2}", t.DN, dn.RDN, t.ExpectedRDN);
                    }
                    if(dn.Parent.CompareTo(t.ExpectedParent) != 0)
                    {
                        Console.WriteLine("Failed: DN [{0}] Parent {1} expected {2}", t.DN, dn.Parent, t.ExpectedParent);
                    }
                    if(dn.Name.CompareTo(t.ExpectedName) != 0)
                    {
                        Console.WriteLine("Failed: DN [{0}] Name {1} expected {2}", t.DN, dn.Name, t.ExpectedName);
                    }
                    // Should ignore case on the compare
                    if(dn.NameType.CompareTo(t.ExpectedNameType) != 0)
                    {
                        Console.WriteLine("Failed: DN [{0}] NameType {1} expected {2}", t.DN, dn.NameType, t.ExpectedNameType);
                    }
                    if(dn.Depth != t.ExpectedDepth)
                    {
                        Console.WriteLine("Failed: DN [{0}] Depth {1} expected {2}", t.DN, dn.Depth, t.ExpectedDepth);
                    }
                    if(!t.ExpectedParentHierarchy.IsEqualTo<LDIFDistinguishedName>(dn.ParentHierarchy, new LDIFDistinguishedName.EqualityComparator()))
                    {
                        Console.WriteLine(dn.ParentHierarchy.Join(";"));
                        Console.WriteLine(t.ExpectedParentHierarchy.Join(";"));
                    }
                }
                catch(System.Exception e)
                {
                    if(t.ExpectedException == null || e.GetType() != t.ExpectedException)
                    {
                        Console.WriteLine("Failed: DN [{0}] Caught exception {1} when expecting {2}", t.DN, e.GetType(), t.ExpectedException == null ? "no exception" : t.ExpectedException.ToString());
                    }
                }
            }
        }
    }
	public static class CollectionTools
	{
		/// <summary>
		/// Join each item in the collection together with the glue string, returning a single string
		/// Each object in the collection will be converted to string with ToString()
		/// </summary>
		/// <param name="collection"></param>
		/// <param name="glue"></param>
		/// <returns></returns>
		public static string Join(this ICollection collection, string glue)
		{
			string returnVal = null;
			if (collection != null)
			{
				foreach (object o in collection)
				{
					if (returnVal == null)
					{
						returnVal = o.ToString();
					}
					else
					{
						returnVal = returnVal + glue + o;
					}
				}
			}
			return returnVal;
		}


		/// <summary>
		/// Checks whether a collection is the same as another collection
		/// </summary>
		/// <param name="value">The current instance object</param>
		/// <param name="compareList">The collection to compare with</param>
		/// <param name="comparer">The comparer object to use to compare each item in the collection.  If null uses EqualityComparer(T).Default</param>
		/// <returns>True if the two collections contain all the same items in the same order</returns>
		public static bool IsEqualTo<TSource>(this IEnumerable<TSource> value, IEnumerable<TSource> compareList, IEqualityComparer<TSource> comparer)
		{
			if (value == compareList)
			{
				return true;
			}
			else if (value == null || compareList == null)
			{
				return false;
			}
			else
			{
				if (comparer == null)
				{
					comparer = EqualityComparer<TSource>.Default;
				}

				IEnumerator<TSource> enumerator1 = value.GetEnumerator();
				IEnumerator<TSource> enumerator2 = compareList.GetEnumerator();

				bool enum1HasValue = enumerator1.MoveNext();
				bool enum2HasValue = enumerator2.MoveNext();

				try
				{
					while (enum1HasValue && enum2HasValue)
					{
						if (!comparer.Equals(enumerator1.Current, enumerator2.Current))
						{
							return false;
						}

						enum1HasValue = enumerator1.MoveNext();
						enum2HasValue = enumerator2.MoveNext();
					}

					return !(enum1HasValue || enum2HasValue);
				}
				finally
				{
					if (enumerator1 != null) enumerator1.Dispose();
					if (enumerator2 != null) enumerator2.Dispose();
				}
			}
		}

		/// <summary>
		/// Checks whether a collection is the same as another collection
		/// </summary>
		/// <param name="value">The current instance object</param>
		/// <param name="compareList">The collection to compare with</param>
		/// <returns>True if the two collections contain all the same items in the same order</returns>
		public static bool IsEqualTo<TSource>(this IEnumerable<TSource> value, IEnumerable<TSource> compareList)
		{
			return IsEqualTo(value, compareList, null);
		}

		/// <summary>
		/// Checks whether a collection is the same as another collection
		/// </summary>
		/// <param name="value">The current instance object</param>
		/// <param name="compareList">The collection to compare with</param>
		/// <returns>True if the two collections contain all the same items in the same order</returns>
		public static bool IsEqualTo(this IEnumerable value, IEnumerable compareList)
		{
			return IsEqualTo<object>(value.OfType<object>(), compareList.OfType<object>());
		}
	}
}
