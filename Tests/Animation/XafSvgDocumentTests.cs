using System;
using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

[TestFixture]
public sealed class XafSvgDocumentTests
{
    [Test]
    public void KeepsOnlyTheRequestedGroupAndItsAncestors()
    {
        string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <defs><style>.a{fill:red}</style></defs>
              <g id="body" transform="translate(10 20)">
                <rect id="torso" width="20" height="30"/>
              </g>
              <g id="arm">
                <rect width="10" height="40"/>
              </g>
            </svg>
            """;

        string result = XafSvgDocument.IsolateGroup(svg, "body");

        StringAssert.Contains("id=\"body\"", result);
        StringAssert.Contains("translate(10 20)", result);
        StringAssert.Contains("torso", result);
        StringAssert.DoesNotContain("id=\"arm\"", result);
        StringAssert.Contains("<defs", result);
    }

    [Test]
    public void RejectsMissingGroups()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"body\"/></svg>";

        Assert.Throws<XafFormatException>(() =>
            XafSvgDocument.IsolateGroup(svg, "missing"));
    }

    [Test]
    public void RejectsMalformedXml()
    {
        Assert.Throws<XafFormatException>(() =>
            XafSvgDocument.IsolateGroup("<svg>", "body"));
    }
}
