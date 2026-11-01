using UnityEngine.Scripting;

// Nothing references this assembly (it registers itself at SubsystemRegistration), and Unity does not
// read link.xml files inside packages, so without this the managed linker strips it whole and the
// player falls back to the stub. AlwaysLinkAssembly makes it a root; [Preserve] keeps the registration
// method and the AndroidJavaProxy listener callbacks, which only Java calls.
[assembly: AlwaysLinkAssembly]
