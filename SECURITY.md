# Security Policy

## Supported Versions

HtmlTemplateEngine follows a rolling-release model. Security fixes are applied to the latest stable version published on NuGet.

Users are encouraged to upgrade to the latest available version to receive security updates and bug fixes.

| Version               | Supported |
| --------------------- | --------- |
| Latest stable release | ✅         |
| Older releases        | ❌         |

## Reporting a Vulnerability

The HtmlTemplateEngine project takes security issues seriously. If you discover a security vulnerability, please report it privately rather than creating a public GitHub issue.

### How to Report

Please create a private security advisory through GitHub's Security Advisories feature or contact the maintainer directly with the following information:

* Description of the vulnerability
* Steps to reproduce
* Proof-of-concept code (if available)
* Potential impact
* Suggested mitigation (optional)

### What to Expect

After a report is received:

1. An acknowledgment will typically be provided within 7 days.
2. The vulnerability will be investigated and validated.
3. If confirmed, a fix will be developed and released as soon as reasonably possible.
4. Coordinated disclosure may occur after a patch is available.

### Scope

Examples of security-related issues include:

* Template injection vulnerabilities
* Arbitrary code execution
* Unauthorized file access
* Remote code execution (RCE)
* Cross-site scripting (XSS) caused by template rendering
* Denial-of-service (DoS) vulnerabilities
* Dependency-related security issues

### Out of Scope

The following are generally not considered security vulnerabilities:

* Issues requiring modification of the library source code
* Vulnerabilities in third-party applications using the library incorrectly
* Feature requests or performance improvements
* Non-exploitable theoretical issues without a practical attack scenario

## Security Best Practices

When using HtmlTemplateEngine:

* Always validate and sanitize untrusted input.
* Avoid rendering templates from untrusted sources unless explicitly reviewed.
* Keep dependencies and package versions up to date.
* Follow the principle of least privilege when accessing files or resources.

Thank you for helping make HtmlTemplateEngine safer for the community.
