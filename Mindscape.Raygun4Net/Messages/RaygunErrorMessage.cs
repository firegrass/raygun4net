using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

#if IOS
using MonoTouch.Foundation;
using MonoTouch.ObjCRuntime;
#endif

namespace Mindscape.Raygun4Net.Messages
{
  public class RaygunErrorMessage
  {
    public RaygunErrorMessage InnerError { get; set; }

    public IDictionary Data { get; set; }

    public string ClassName { get; set; }

    public string Message { get; set; }

    public RaygunErrorStackTraceLineMessage[] StackTrace { get; set; }

    public RaygunErrorMessage()
    {
    }

    public RaygunErrorMessage(Exception exception)
    {
      var exceptionType = exception.GetType();

#if IOS
      MonoTouchException mex = exception as MonoTouchException;
      if(mex != null && mex.NSException != null)
      {
        Message = string.Format("{0}: {1}", mex.NSException.Name, mex.NSException.Reason);
        ClassName = mex.NSException.Name;
      }
      else{
#endif
      Message = string.Format("{0}: {1}", exceptionType.Name, exception.Message);
      ClassName = exceptionType.FullName;
#if IOS
      }
#endif

      StackTrace = BuildStackTrace(exception);
      Data = exception.Data;

      if (exception.InnerException != null)
      {
        InnerError = new RaygunErrorMessage(exception.InnerException);
      }
    }

    private RaygunErrorStackTraceLineMessage[] BuildStackTrace(Exception exception)
    {      
      var lines = new List<RaygunErrorStackTraceLineMessage>();

#if WINRT
      string[] delim = { "\r\n" };
      string stackTrace = exception.StackTrace ?? exception.Data["Message"] as string;      
      if (stackTrace != null)
      {
        var frames = stackTrace.Split(delim, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in frames)
        {
          lines.Add(new RaygunErrorStackTraceLineMessage()
            {
              ClassName = line
            });
        }
      }
#elif WINDOWS_PHONE
      if (exception.StackTrace != null)
      {
        char[] delim = {'\r', '\n'};
        var frames = exception.StackTrace.Split(delim);
        foreach (string line in frames)
        {
          if (!"".Equals(line))
          {
            RaygunErrorStackTraceLineMessage stackTraceLineMessage = new RaygunErrorStackTraceLineMessage();
            stackTraceLineMessage.ClassName = line;
            lines.Add(stackTraceLineMessage);
          }
        }
      }
#else
#if IOS
      MonoTouchException mex = exception as MonoTouchException;
      if (mex != null && mex.NSException != null)
      {
        var ptr = Messaging.intptr_objc_msgSend (mex.NSException.Handle, Selector.GetHandle ("callStackSymbols"));
        var arr = NSArray.StringArrayFromHandle (ptr);
        foreach( var line in arr)
        {
          lines.Add(new RaygunErrorStackTraceLineMessage{ FileName = line});
        }
        return lines.ToArray();
      }
      string stackTraceStr = exception.StackTrace;
      if (stackTraceStr == null)
      {
        var line = new RaygunErrorStackTraceLineMessage { FileName = "none", LineNumber = 0 };
        lines.Add(line);
        return lines.ToArray();
      }
      try
      {
        string[] stackTraceLines = stackTraceStr.Split('\n');
        foreach (string stackTraceLine in stackTraceLines)
        {
          int lineNumber = 0;
          string fileName = null;
          string methodName = null;
          string className = null;
          string stackTraceLn = stackTraceLine;
          // Line number
          int index = stackTraceLine.LastIndexOf(":");
          if (index > 0)
          {
            bool success = int.TryParse(stackTraceLn.Substring(index + 1), out lineNumber);
            if(success)
            {
              stackTraceLn = stackTraceLn.Substring(0, index);
              // File name
              index = stackTraceLn.LastIndexOf("] in ");
              if (index > 0)
              {
                fileName = stackTraceLn.Substring(index + 5);
                stackTraceLn = stackTraceLn.Substring(0, index);
                // Method name
                index = stackTraceLn.LastIndexOf("(");
                if (index > 0)
                {
                  index = stackTraceLn.LastIndexOf(".", index);
                  if (index > 0)
                  {
                    int endIndex = stackTraceLn.IndexOf("[0x");
                    if (endIndex < 0)
                    {
                      endIndex = stackTraceLn.Length;
                    }
                    methodName = stackTraceLn.Substring(index + 1, endIndex - index - 1).Trim();
                    methodName = methodName.Replace(" (", "(");
                    stackTraceLn = stackTraceLn.Substring(0, index);
                  }
                }
                // Class name
                index = stackTraceLn.IndexOf("at ");
                if (index >= 0)
                {
                  className = stackTraceLn.Substring(index + 3);
                }
              }
              else
              {
                fileName = stackTraceLn;
              }
            }
            else
            {
              index = stackTraceLn.IndexOf("at ");
              if(index >= 0)
              {
                index += 3;
              }
              else
              {
                index = 0;
              }
              fileName = stackTraceLn.Substring(index);
            }
          }
          else
          {
            fileName = stackTraceLn;
          }
          var line = new RaygunErrorStackTraceLineMessage
          {
            FileName = fileName,
            LineNumber = lineNumber,
            MethodName = methodName,
            ClassName = className
          };

          lines.Add(line);
        }
        if(lines.Count > 0)
        {
          return lines.ToArray();
        }
      }
      catch {}
#endif
      var stackTrace = new StackTrace(exception, true);
      var frames = stackTrace.GetFrames();

      if (frames == null || frames.Length == 0)
      {
        var line = new RaygunErrorStackTraceLineMessage { FileName = "none", LineNumber = 0 };
        lines.Add(line);
        return lines.ToArray();
      }

      foreach (StackFrame frame in frames)
      {
        MethodBase method = frame.GetMethod();

        if (method != null)
        {
          int lineNumber = frame.GetFileLineNumber();

          if (lineNumber == 0)
          {
            lineNumber = frame.GetILOffset();
          }

          var methodName = GenerateMethodName(method);

          string file = frame.GetFileName();

          string className = method.ReflectedType != null
                       ? method.ReflectedType.FullName
                       : "(unknown)";

          var line = new RaygunErrorStackTraceLineMessage
          {
            FileName = file,
            LineNumber = lineNumber,
            MethodName = methodName,
            ClassName = className
          };

          lines.Add(line);
        }
      }
#endif
      return lines.ToArray();
    }

    private string GenerateMethodName(MethodBase method)
    {
      var stringBuilder = new StringBuilder();

      stringBuilder.Append(method.Name);

      if (method is MethodInfo && method.IsGenericMethod)
      {
        Type[] genericArguments = method.GetGenericArguments();
        stringBuilder.Append("[");
        int index2 = 0;
        bool flag2 = true;
        for (; index2 < genericArguments.Length; ++index2)
        {
          if (!flag2)
            stringBuilder.Append(",");
          else
            flag2 = false;
          stringBuilder.Append(genericArguments[index2].Name);
        }
        stringBuilder.Append("]");
      }
      stringBuilder.Append("(");
      ParameterInfo[] parameters = method.GetParameters();
      bool flag3 = true;
      for (int index2 = 0; index2 < parameters.Length; ++index2)
      {
        if (!flag3)
          stringBuilder.Append(", ");
        else
          flag3 = false;
        string str2 = "<UnknownType>";
        if (parameters[index2].ParameterType != null)
          str2 = parameters[index2].ParameterType.Name;
        stringBuilder.Append(str2 + " " + parameters[index2].Name);
      }
      stringBuilder.Append(")");
      
      return stringBuilder.ToString();
    }
  }
}
