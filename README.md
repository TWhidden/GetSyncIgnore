# GetSyncIgnore
For those using GetSync to sync across different machine, and want to have a simple UI to exclude folders (later will add files) without editing the ignore file directly. 


![alt text](https://raw.githubusercontent.com/TWhidden/GetSyncIgnore/master/Images/ui.png "Main UI")


![alt text](https://raw.githubusercontent.com/TWhidden/GetSyncIgnore/master/Images/IgnoreList.png "Resulting IgnoreList")

Few Notes: 
1.) Make sure to select the correct system type (Linux/Windows). This impacts which way the path vars are.  For example, if you are on a windows machine, editing the ignore list located on a linux machine, you will want to adjust accordingly. 
2.) Currently, I dont have a prompt to ask you, are you sure you want to exclude.  This will DELETE everything selected. Eventually, I will have it be an option to allow you to purge the selected folders.
3.) The idea for this is more for the remote users that get a copy of your BTSync (such as a Read-Only). For example, if you share a media folder, and someone doesnt want ALL of your media; just some. 
4.) This is compilied for Windows with .net 4.6 using WPF.  You are welecome to pull it down, and compile for something else.
5.) Hope this is helpful to someone. I'll consider good pull requests. ;)