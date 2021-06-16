# SyncMLViewer

This tool is able to present the SyncML protocol stream between the Windows 10 client and management system. In addition it does some extra parsing to extract details and make the analyzing a bit easier.

The tool uses ETW to trace the MDM Sync session. In general the tool can be very handy to troubleshoot policy issues. Tracing what the client actually sends and receives provides deep protocol insights. Verifying OMA-URIs and data field definitions. 
It makes it easy to get confirmation about queried or applied settings. 

![SyncML Viewer application](https://raw.githubusercontent.com/okieselbach/SyncMLViewer/master/SyncMLViewer/SyncMLViewer.png)

SyncML Viewer download as zip archive can be found under subfolder **SyncMLViewer/dist**

Happy tracing!

The tool supports manual online updates. When a new version is available it will be indicated.
Use *Menu Item > Help > Check for SyncML Viewer Update* to trigger a download.

I'm happy to take feedback. The easiest way is to create an issue here at the GitHub solution. 
The tool is far away from good developer coding practice :-), but for the small helper sufficient enough. I followed no design pattern like MVVM and all logic is in the code behind.

I have written an introduction blog article about the tool here:  
https://oliverkieselbach.com/2019/10/11/windows-10-mdm-client-activity-monitoring-with-syncml-viewer
\
\
\
Credits:\
\
Inspired by Michael Niehaus (@mniehaus) - blog about monitoring realtime MDM activity  
https://oofhours.com/2019/07/25/want-to-watch-the-mdm-client-activity-in-real-time/

All possible due to Event Tracing for Windows (ETW)  
https://docs.microsoft.com/en-us/windows/win32/etw/event-tracing-portal

Special thanks to Matt Graeber (@mattifestation) - for the published extended ETW Provider list  
...without this info the tool wouldn't be possible for me to write!  
https://gist.github.com/mattifestation/04e8299d8bc97ef825affe733310f7bd/

More MDM ETW Provider details  
https://docs.microsoft.com/en-us/windows/client-management/mdm/diagnose-mdm-failures-in-windows-10  

[MS-MDM]: Mobile Device Management Protocol  
https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-mdm/

OMA DM protocol support - Get all the details how it is working...  
https://docs.microsoft.com/en-us/windows/client-management/mdm/oma-dm-protocol-support

SyncML response status codes  
https://docs.microsoft.com/en-us/windows/client-management/mdm/oma-dm-protocol-support#syncml-response-codes  
http://openmobilealliance.org/release/Common/V1_2_2-20090724-A/OMA-TS-SyncML-RepPro-V1_2_2-20090724-A.pdf

UI Controls inspired by ILspy (https://github.com/icsharpcode/ILSpy) and the controls used there:  

AvalonEdit  
http://avalonedit.net/  
released under MIT License (https://opensource.org/licenses/MIT)
