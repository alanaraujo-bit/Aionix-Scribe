import { Hero } from "@/components/Hero";
import { Features, HowItWorks, Privacy } from "@/components/Sections";
import { Pricing } from "@/components/Pricing";
import { Nav, FinalCta, Footer } from "@/components/Chrome";

export default function Home() {
  return (
    <>
      <Nav />
      <main className="relative">
        <Hero />
        <HowItWorks />
        <Features />
        <Privacy />
        <Pricing />
        <FinalCta />
      </main>
      <Footer />
    </>
  );
}
