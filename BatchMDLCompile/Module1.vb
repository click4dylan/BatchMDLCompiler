Module Module1
    'todo: restore backups
    Public errors As New List(Of String)
    Sub Main()
        Console.Title = "Batch Compile Valve MDLs (logs errors)"

        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        Dim directory As String = "I:\Program Files (x86)\Steam\steamapps\sourcemods\BATCH_COMPILE"
        Dim studiomdl As String = "I:\Program Files (x86)\Steam\steamapps\common\Source SDK Base 2013 Multiplayer\bin\studiomdl.exe"
        Console.WriteLine("Please enter the path containing models to be compiled.")
        Dim output = Console.ReadLine
        If Not output = Nothing Then directory = output.Replace("/", "\")
        If directory.EndsWith("\") Then directory = directory.Remove(directory.Length - 1)
        Console.WriteLine("Please enter the path to your studiomdl.exe")
        output = Console.ReadLine()
        If Not output = Nothing Then studiomdl = studiomdl.Replace("/", "\")
        If Not studiomdl.EndsWith("studiomdl.exe") Then
            If Not studiomdl.EndsWith("\") Then studiomdl = studiomdl & "\"
            studiomdl = studiomdl & "studiomdl.exe"
        End If
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        Console.WriteLine("Attempting to add Collision Models to QC files")
        AddCollisionModels(directory)
        If errors.Count > 0 Then
            WriteErrorLog(directory, "COLLISION_ERRORS.log")
            Console.WriteLine("Proceeding to compile models in 5 seconds...")
            Threading.Thread.Sleep(1000 * 5)
            errors.Clear()
            'Exit Sub
        End If

        CompileModels(directory, studiomdl)
        WriteErrorLog(directory, "COMPILE_ERRORS.log")
        Threading.Thread.Sleep(1000 * 10)
    End Sub

    Private Sub CompileModels(ByVal directory As String, ByVal studiomdl As String)
        For Each file In IO.Directory.GetFiles(directory, "*.qc", IO.SearchOption.AllDirectories)
            If Not file.EndsWith("_backup.qc") Then
                file = file.Replace(directory & "\", "")
                Dim compiler As New Process
                compiler.StartInfo.FileName = studiomdl
                compiler.StartInfo.WorkingDirectory = directory
                compiler.StartInfo.Arguments = "-nop4 " & file
                compiler.StartInfo.UseShellExecute = False
                compiler.StartInfo.RedirectStandardOutput = True
                compiler.Start()
                Dim SROutput As System.IO.StreamReader = compiler.StandardOutput
                Dim tmp As String = ""
RestartStream:  'glorious shit code
                While SROutput.Peek > 0
                    Dim line = SROutput.ReadLine
                    Console.WriteLine(line)
                    Dim a = line.Split(vbNewLine)
                    For Each thing In a
                        If thing.StartsWith("ERROR") Then
                            errors.Add(thing)
                        End If
                    Next
                End While
                SROutput.DiscardBufferedData()
                If Not compiler.HasExited() Then
                    GoTo RestartStream 'THERE IS A BETTER WAY TO DO THIS!!
                End If
                compiler.Dispose()
            End If
        Next
    End Sub
    Private Sub WriteErrorLog(ByVal directory As String, ByVal logname As String)
        If errors.Count > 0 Then
            Console.Clear()
            Console.WriteLine("Completed with " & errors.Count & " errors!" & vbNewLine & "Check " & directory & "\" & logname & " for details")
            Dim logfile = directory & "\" & logname
            If IO.File.Exists(logfile) Then
                IO.File.Delete(logfile)
            End If
            Dim fs As New IO.FileStream(logfile, IO.FileMode.Create)
            Dim sw As New IO.StreamWriter(fs)
            For Each line In errors
                sw.WriteLine(line)
            Next
            sw.Close()
            fs.Close()
        Else
            Console.WriteLine(vbNewLine & "Completed without error!")
        End If
    End Sub
    Private Sub AddCollisionModels(ByVal directory As String)
        Dim dictionary As New Dictionary(Of String, String) 'directory, qc
        For Each root In IO.Directory.GetFiles(directory, "*.qc", IO.SearchOption.AllDirectories)
            Dim qc = root.Replace(directory & "\", "")
            Dim spl = qc.Split("\")
            qc = spl(spl.Length - 1)
            root = root.Substring(0, root.Length - qc.Length)
            If Not root.EndsWith("\") Then root = root & "\"
            'If root.EndsWith("\") Then root = root.Substring(0, root.Length - 1)
            If Not dictionary.ContainsKey(root) Then dictionary.Add(root, qc)
        Next

        For Each dat In dictionary
            'If dat.Value.ToLower.Contains("switch_gate") Then Debugger.Break()
            Dim editedqcdata As List(Of String) = ReplaceData(LoadQC(dat.Key, dat.Value), dat.Key & dat.Value)
            If editedqcdata.Count > 0 Then
                WriteNewQC(dat.Key & dat.Value, editedqcdata)
            End If
        Next
    End Sub
    Private Function ReplaceData(ByVal stringdict As List(Of String), ByVal filenameforerrorchecking As String) As List(Of String) 'replaces data
        If Not stringdict.Count = 0 Then
            Dim alreadyhascollisionmodel As Boolean = False
restartfor:
            For Each line In stringdict
                If line.Contains("$collisionmodel") Then
                    alreadyhascollisionmodel = True
                    Exit For
                End If
                If line.StartsWith("$staticprop") Then
                    Dim index = stringdict.IndexOf(line)
                    stringdict(index) = "//$staticprop"
                    GoTo restartfor
                End If
            Next
            Dim listofsmds As List(Of String)
            If Not alreadyhascollisionmodel Then
                listofsmds = RetrieveString(stringdict, "$bodygroup")
                If listofsmds.Count = 1 Then
                    Console.WriteLine("Adding collision model: " & filenameforerrorchecking)
                    stringdict.Add("$collisionmodel " & ControlChars.Quote & listofsmds(0) & ControlChars.Quote)
                    stringdict.Add("{")
                    stringdict.Add(ControlChars.Tab & "$automass")
                    stringdict.Add("}")
                Else
                    Dim success = False
                    For Each a In listofsmds
                        a = a.ToLower
                        Dim spl = filenameforerrorchecking.Split("\")
                        Dim qcname = spl(spl.Length - 1)
                        qcname = qcname.Substring(0, qcname.Length - 3).ToLower
'TODO: REWRITE THIS BETTER!
                        If a.Contains("lod0") And a.Contains("body") Then
                            Console.WriteLine("Adding lod0 collision model: " & filenameforerrorchecking)
                            stringdict.Add("$collisionmodel " & ControlChars.Quote & a & ControlChars.Quote)
                            stringdict.Add("{")
                            stringdict.Add(ControlChars.Tab & "$automass")
                            stringdict.Add("}")
                            success = True
                        ElseIf a.Contains(qcname & "_ref") Or a.Contains(qcname & "_reference_ref") Or a.Contains(qcname & "_lod0_ref") Or _
                        a.Contains(qcname & "_lod0_reference_ref") Or a.Contains(qcname & "_ref_ref") Or _
                        a.Contains(qcname & "_01_reference_ref") Or a.Contains(qcname & "_01_lod0_ref") Or _
                        a.Contains(qcname & "_01_ref") Or a.Contains(qcname & "_01_REFERENCE_ref") Then
                            Console.WriteLine("Adding ref collision model: " & filenameforerrorchecking)
                            stringdict.Add("$collisionmodel " & ControlChars.Quote & a & ControlChars.Quote)
                            stringdict.Add("{")
                            stringdict.Add(ControlChars.Tab & "$automass")
                            stringdict.Add("}")
                            success = True
                        ElseIf qcname.EndsWith("s") Then
                            Dim qcname2 = qcname.Substring(0, qcname.Length - 1)
                            If a.Contains(qcname2 & "_ref") Or a.Contains(qcname2 & "_reference_ref") Or a.Contains(qcname2 & "_lod0_ref") Or _
                            a.Contains(qcname2 & "_lod0_reference_ref") Or a.Contains(qcname2 & "_ref_ref") Or _
                            a.Contains(qcname2 & "_01_reference_ref") Or a.Contains(qcname2 & "_01_lod0_ref") Or _
                            a.Contains(qcname2 & "_01_ref") Or a.Contains(qcname2 & "_01_REFERENCE_ref") Then
                                Console.WriteLine("Adding ref collision model: " & filenameforerrorchecking)
                                stringdict.Add("$collisionmodel " & ControlChars.Quote & a & ControlChars.Quote)
                                stringdict.Add("{")
                                stringdict.Add(ControlChars.Tab & "$automass")
                                stringdict.Add("}")
                                success = True
                            End If
                        End If
                        If success Then Exit For
                    Next
                    If Not success Then
                        'Console.WriteLine("ERROR: > 1 bodygroup sequence: " & filenameforerrorchecking)
                        errors.Add("ERROR: > 1 bodygroup sequence: " & filenameforerrorchecking)
                        'TODO: algorithm to detect proper smd
                    End If
                End If
            End If
        End If
        Return stringdict
    End Function
    Private Function RetrieveString(ByVal dict As List(Of String), ByVal retrieve As String) As List(Of String)
        Dim temp As New List(Of String)
        For Each line In dict
            Dim index
            If line.StartsWith(retrieve) Then 'todo: support same line bodygroup studioname
                index = dict.IndexOf(line)
                For i As Integer = index + 1 To dict.Count
                    If dict(i) = "}" Then Exit For
                    If Not dict(i) = "{" And Not dict(i) = "blank" And dict(i).Contains("studio") Then
                        Dim spl = dict(i).Split(ControlChars.Quote)
                        temp.Add(spl(1))
                    End If
                Next
            End If
        Next
        Return temp
    End Function
    Private Function LoadQC(ByVal dir As String, ByVal qc As String) As List(Of String)
        Dim entireqc As New List(Of String)
        If Not IO.File.Exists(dir & qc) Then
            Console.WriteLine("ERROR: " & qc & " does not exist")
            Threading.Thread.Sleep(1000)
            Return Nothing
        Else
            Dim fs As New IO.FileStream(dir & qc, IO.FileMode.Open)
            Dim sr As New IO.StreamReader(fs)
            While sr.Peek > 0
                Dim line = sr.ReadLine
                entireqc.Add(line)
            End While
            sr.Close()
            fs.Close()
        End If
        Return entireqc
    End Function 'loads file
    Private Sub WriteNewQC(ByVal file As String, ByVal qcdata As List(Of String))
        Dim backupfile As String = file.Replace(".qc", "" & "_backup.qc")
        If Not IO.File.Exists(backupfile) Then
            IO.File.Copy(file, backupfile) 'MAKE A BACKUP FIRST
        End If
        If IO.File.Exists(file) Then IO.File.Delete(file)
        Dim fs As New IO.FileStream(file, IO.FileMode.Create)
        Dim sw As New IO.StreamWriter(fs)
        For Each line In qcdata
            sw.WriteLine(line)
        Next
        sw.Close()
        fs.Close()
    End Sub
End Module
