Name: fut
Version: 3.1.2
Release: 1
Summary: Fusion Transpiler
License: GPLv3+
Source: https://github.com/fusionlanguage/fut/archive/fut-%{version}/fut-%{version}.tar.gz
URL: https://github.com/fusionlanguage/fut
BuildRequires: g++ >= 13

%description
Transpiles the Fusion programming langauge to
C, C++, C#, D, Java, JavaScript, Python, Swift, TypeScript and OpenCL C.

%prep
%setup -q

%build
make CXXFLAGS="%{build_cxxflags}"

%install
make DESTDIR=%{buildroot} prefix=%{_prefix} install

%files
%{_bindir}/fut

%changelog
* Mon Mar 4 2024 Piotr Fusik <piotr@fusion-lang.org>
- 3.1.2-1

* Mon Dec 18 2023 Piotr Fusik <piotr@fusion-lang.org>
- 3.1.1-1

* Thu Nov 16 2023 Piotr Fusik <piotr@fusion-lang.org>
- 3.1.0-1

* Fri Oct 20 2023 Piotr Fusik <piotr@fusion-lang.org>
- 3.0.2-1

* Tue Aug 22 2023 Piotr Fusik <piotr@fusion-lang.org>
- 3.0.1-1

* Thu Aug 3 2023 Piotr Fusik <piotr@fusion-lang.org>
- 3.0.0-1
- Initial packaging
