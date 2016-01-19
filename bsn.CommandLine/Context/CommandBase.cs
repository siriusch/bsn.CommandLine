﻿// bsn CommandLine Library
// -----------------------
// 
// Copyright 2014 by Arsène von Wyss - avw@gmx.ch
// 
// Development has been supported by Sirius Technologies AG, Basel
// 
// Source:
// 
// https://bsn-commandline.googlecode.com/hg/
// 
// License:
// 
// The library is distributed under the GNU Lesser General Public License:
// http://www.gnu.org/licenses/lgpl.html
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//  
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

using bsn.GoldParser.Text;

namespace bsn.CommandLine.Context {
	public abstract class CommandBase<TExecutionContext>: ContextItem<TExecutionContext> where TExecutionContext: class, IExecutionContext<TExecutionContext> {
		private readonly CommandBase<TExecutionContext> parentCommand;

		protected CommandBase(CommandBase<TExecutionContext> parentCommand) {
			this.parentCommand = parentCommand;
		}

		public CommandBase<TExecutionContext> ParentCommand {
			get {
				return parentCommand;
			}
		}

		public ContextBase<TExecutionContext> ParentContext {
			get {
				for (CommandBase<TExecutionContext> command = parentCommand; command != null; command = command.parentCommand) {
					ContextBase<TExecutionContext> result = command as ContextBase<TExecutionContext>;
					if (result != null) {
						return result;
					}
				}
				return null;
			}
		}

		public abstract void Execute(TExecutionContext executionContext, IDictionary<string, object> tags);

		public virtual IEnumerable<CommandBase<TExecutionContext>> GetAvailableCommands() {
			yield break;
		}

		public virtual IEnumerable<ITagItem<TExecutionContext>> GetCommandTags() {
			yield break;
		}

		public override void WriteItemHelp(RichTextWriter writer, TExecutionContext executionContext) {
			writer.WriteLine(Description);
			List<ITagItem<TExecutionContext>> parameters = new List<ITagItem<TExecutionContext>>(GetCommandTags());
			if (parameters.Count > 0) {
				writer.WriteLine();
				writer.WriteLine("Usage:");
				WriteCommandName(writer);
				foreach (ITagItem<TExecutionContext> tag in parameters) {
					writer.Write(" [");
					writer.Write(tag.Name);
					writer.Write("=]");
					bool optional = tag.GetOptional(executionContext);
					if (optional) {
						writer.Write('[');
					}
					writer.Write(tag.PatternHelp);
					if (optional) {
						writer.Write(']');
					}
				}
				writer.WriteLine();
				writer.WriteLine();
				writer.WriteLine("Parameters:");
				foreach (ITagItem<TExecutionContext> parameter in parameters) {
					writer.WriteLine(" {0,-14} - {1}", parameter.Name, parameter.Description);
				}
			}
			NamedItemAttribute attribute;
			if (TryGetNameAttribute(GetType(), out attribute) && (!string.IsNullOrEmpty(attribute.Remarks))) {
				writer.WriteLine();
				writer.WriteLine("Remarks:");
				writer.WriteLine(attribute.Remarks);
			}
			using (IEnumerator<CommandBase<TExecutionContext>> enumerator = GetAvailableCommands().GetEnumerator()) {
				if (enumerator.MoveNext()) {
					writer.WriteLine();
					writer.WriteLine("Available subcommands:");
					do {
						CommandBase<TExecutionContext> current = enumerator.Current;
						Debug.Assert(current != null);
						writer.Write(' ');
						current.WriteNameLine(writer, Name);
					} while (enumerator.MoveNext());
				}
			}
		}

		internal void ExecuteInternal(TExecutionContext executionContext, IDictionary<string, string> namedTags, IList<string> unnamedTags) {
			Dictionary<string, object> tags = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			foreach (ITagItem<TExecutionContext> tag in GetCommandTags()) {
				string stringValue;
				if (namedTags.TryGetValue(tag.Name, out stringValue)) {
					namedTags.Remove(tag.Name);
				} else if (unnamedTags.Count > 0) {
					stringValue = unnamedTags[0];
					unnamedTags.RemoveAt(0);
				}
				if (stringValue == null) {
					object defaultValue;
					bool useDefault = tag.TryGetDefault(executionContext, out defaultValue);
					if (!tag.GetOptional(executionContext)) {
						StringBuilder prompt = new StringBuilder();
						prompt.Append(' ');
						prompt.Append(tag.Name);
						if (useDefault) {
							prompt.AppendFormat(CultureInfo.InvariantCulture, " [{0}]", defaultValue);
						}
						prompt.Append(": ");
						executionContext.Output.Write(prompt);
						stringValue = executionContext.Input.ReadLine();
						useDefault &= string.IsNullOrEmpty(stringValue);
						if (!useDefault) {
							tags.Add(tag.Name, tag.ParseValue(executionContext, stringValue));
						}
					}
					if (useDefault) {
						tags.Add(tag.Name, defaultValue);
					}
				} else {
					tags.Add(tag.Name, tag.ParseValue(executionContext, stringValue));
				}
			}
			if ((namedTags.Count+unnamedTags.Count) > 0) {
				StringBuilder message = new StringBuilder("Excess tags received:");
				foreach (string unnamedTag in unnamedTags) {
					message.Append(" \"");
					message.Append(unnamedTag.Replace("\"", "\"\""));
					message.Append("\"");
				}
				foreach (KeyValuePair<string, string> namedTag in namedTags) {
					message.Append(' ');
					message.Append(namedTag.Key);
					message.Append("=\"");
					message.Append(namedTag.Value.Replace("\"", "\"\""));
					message.Append("\"");
				}
				throw new InvalidOperationException(message.ToString());
			}
			Execute(executionContext, tags);
		}

		private void WriteCommandName(RichTextWriter writer) {
			if ((parentCommand != null) && (!(parentCommand is ContextBase<TExecutionContext>))) {
				parentCommand.WriteCommandName(writer);
			}
			writer.Write(' ');
			writer.Write(Name);
		}
	}
}
