using Microsoft.AspNetCore.Http;
using System.Collections;
using System.Collections.Generic;

public class FormFileCollection : List<IFormFile>, IFormFileCollection
{
	public IFormFile this[string name]
	{
		get
		{
			return this.Find(file => file.Name == name);
		}
	}

	public FormFileCollection() : base()
	{
	}

	public FormFileCollection(IEnumerable<IFormFile> files) : base(files)
	{
	}

	IEnumerator<IFormFile> IEnumerable<IFormFile>.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	public IFormFile GetFile(string name)
	{
		throw new NotImplementedException();
	}

	public IReadOnlyList<IFormFile> GetFiles(string name)
	{
		throw new NotImplementedException();
	}
}
