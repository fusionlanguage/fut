Name: cito
Version: 3.0.0
Release: 1
Summary: Ć Transpiler
License: GPLv3+
Source: https://github.com/pfusik/cito/archive/cto-%{version}/cito-%{version}.tar.gz
URL: https://github.com/pfusik/cito
BuildRequires: gcc >= 13

%description
Transpiles the Ć programming langauge to
C, C++, C#, D, Java, JavaScript, Python, Swift, TypeScript and OpenCL C.

%prep
%setup -q

%build
make CXXFLAGS="%{build_cxxflags}"

%install
make DESTDIR=%{buildroot} prefix=%{_prefix} install

%files
%{_bindir}/cito

%changelog
* Tue Jul 11 2023 Piotr Fusik <fox@scene.pl>
- 3.0.0-1
- Initial packaging
