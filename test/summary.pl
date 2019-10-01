#!perl
use strict;
my %t;
my %p;
for (@ARGV) {
	m!/(\w+)\.txt$! or die;
	my $n = $1;
	$t{$n}++;
	open IN, $_ or die "$_: $!\n";
	$p{$n} += <IN> =~ /^PASSED/;
}
print "PASSED";
print " $_=$p{$_}/$t{$_}" for sort keys %t;
print "\n";
