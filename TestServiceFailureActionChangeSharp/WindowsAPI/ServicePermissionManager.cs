using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace XQ.ConveyorBelt.UpdateService.WindowsAPI
{
	/// <summary>
	/// Helper class for doing specific modifications to the Access Control List associated with a service
	/// </summary>
	static class ServicePermissionManager
	{
		/// <summary>
		/// Alters the security descriptor of the specified service to permit any locally-interactive user, regardless
		/// of whether it has administrative permissions, to request that the service start.
		/// </summary>
		/// <param name="serviceName">The name of the service whose configuration needs to be adjusted.</param>
		public static void AllowAnyInteractiveUserToStartService(string serviceName)
		{
			// Read the service's current service descriptor (specifically the Discretionary Access Control List part).
			IntPtr psidOwner_ignored;
			IntPtr psidGroup_ignored;
			IntPtr pDacl = IntPtr.Zero;
			IntPtr pSacl_ignored;
			IntPtr pSecurityDescriptor = IntPtr.Zero;

			int status = NativeMethods.GetNamedSecurityInfoW(
				serviceName,
				ObjectType.SE_SERVICE,
				SecurityInformation.DACL_SECURITY_INFORMATION,
				out psidOwner_ignored,
				out psidGroup_ignored,
				out pDacl,
				out pSacl_ignored,
				out pSecurityDescriptor);

			if (status != NativeMethods.ERROR_SUCCESS)
				throw new Win32Exception(status);

			try
			{
				// Gather information about the old DACL -- its revision and how large it is.
				int daclRevision;

				bool success = NativeMethods.GetAclInformation(
					pAcl: pDacl,
					pAclInformation: out daclRevision,
					nAclInformationLength: 4,
					dwAclInformationClass: ACLInformationClass.AclRevisionInformation);

				if (!success)
					throw new Win32Exception();

				int[] daclSizeInfo = new int[3];

				success = NativeMethods.GetAclInformation(
					pAcl: pDacl,
					pAclInformation: daclSizeInfo,
					nAclInformationLength: 12,
					dwAclInformationClass: ACLInformationClass.AclSizeInformation);

				if (!success)
					throw new Win32Exception();

				int pDacl_AceCount = daclSizeInfo[0];
				int pDacl_AclBytesInUse = daclSizeInfo[1];
				int pDacl_AclBytesFree = daclSizeInfo[2];

				// Determine the details for a new ACE granting service start access, in case we have to add one. These details
				// will also be used to adjust any existing access-allowed ACE for the "Interactive Users" SID.
				byte[] interactiveLogonSid = NativeMethods.ConvertStringSidToSid("S-1-5-4");

				var allowStartServiceToInteractiveLogonACESize = Marshal.SizeOf(typeof(ACCESS_ALLOWED_ACE)) + interactiveLogonSid.Length;

				// Allocate memory for a copy of the DACL altered to allow SERVICE_START to "Interactive Users".
				int newDaclBytes = pDacl_AclBytesInUse + allowStartServiceToInteractiveLogonACESize;

				if ((newDaclBytes & 3) != 0)
					newDaclBytes += (4 - (newDaclBytes & 3)); // DWORD-align the buffer size

				byte[] newDacl = new byte[newDaclBytes];

				// Initialize an ACL structure in the newly-allocated memory.
				success = NativeMethods.InitializeAcl(newDacl, newDacl.Length, daclRevision);

				if (!success)
					throw new Win32Exception();

				// Copy the existing ACEs from the old DACL to the new structure. If we encounter an "Allow" ACE for the
				// "Interactive Users" SID, we modify it to include the SERVICE_START permission, otherwise we insert a
				// new "Allow" ACE at the appropriate point -- after all "Deny" ACEs and before inherited ACEs, if any.
				bool haveACE = false;

				for (int aceIndex = 0; aceIndex < pDacl_AceCount; aceIndex++)
				{
					IntPtr aceAddr;

					success = NativeMethods.GetAce(pDacl, aceIndex, out aceAddr);

					if (!success)
						throw new Win32Exception();

					var header = (ACE_HEADER)Marshal.PtrToStructure(aceAddr, typeof(ACE_HEADER));

					if (!haveACE)
					{
						if ((header.AceFlags & NativeMethods.INHERITED_ACE) != 0)
						{
							// This is an inherited ACE. All non-inherited ACEs should precede it, so we insert our new "Interactive User" ACE right now.
							success = NativeMethods.AddAccessAllowedAce(newDacl, daclRevision, ServiceAccess.SERVICE_START, interactiveLogonSid);

							if (!success)
								throw new Win32Exception();

							haveACE = true;

							// Flow through to the AddAce below that will copy this inherited ACE, now that we've inserted our new (non-inherited) ACE.
						}
						else if (header.AceType == ACCESS_ALLOWED_ACE.AceType)
						{
							// Check whether this is an ACE for the same user we want to grant a permission to.
							IntPtr sidAddr = aceAddr + Marshal.SizeOf(typeof(ACCESS_ALLOWED_ACE));

							byte[] aceSid = NativeMethods.ReadSid(sidAddr);

							if (aceSid.SequenceEqual(interactiveLogonSid))
							{
								// It is -- so, instead of inserting a new ACE, we twiddle this SID's access mask to include the permission we want as well.
								var accessAllowedAce = (ACCESS_ALLOWED_ACE)Marshal.PtrToStructure(aceAddr, typeof(ACCESS_ALLOWED_ACE));

								success = NativeMethods.AddAccessAllowedAceEx(newDacl, daclRevision, accessAllowedAce.Header.AceFlags, (ServiceAccess)accessAllowedAce.Mask | ServiceAccess.SERVICE_START, interactiveLogonSid);

								if (!success)
									throw new Win32Exception();

								haveACE = true;

								// Skip the AddAce below that will copy this ACE, since we've already done that (with changes).
								continue;
							}
						}
					}

					// Copy this ACE from the old DACL to the new one.
					success = NativeMethods.AddAce(newDacl, daclRevision, dwStartingAceIndex: -1, pAceList: aceAddr, nAceListLength: header.AceSize);

					if (!success)
						throw new Win32Exception();
				}

				if (!haveACE)
					NativeMethods.AddAccessAllowedAce(newDacl, daclRevision, ServiceAccess.SERVICE_START, interactiveLogonSid);

				// Write out the new DACL.
				status = NativeMethods.SetNamedSecurityInfoW(
					serviceName,
					ObjectType.SE_SERVICE,
					SecurityInformation.DACL_SECURITY_INFORMATION,
					psidOwner_ignored,
					psidGroup_ignored,
					newDacl,
					pSacl_ignored);

				if (status != NativeMethods.ERROR_SUCCESS)
					throw new Win32Exception(status);
			}
			finally
			{
				if (pSecurityDescriptor != IntPtr.Zero)
					NativeMethods.LocalFree(pSecurityDescriptor);
			}
		}
	}
}
