FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt update -yq \
    && apt upgrade -yq

RUN apt install -yq \
    curl \
    wget \
    build-essential \
    gnupg

RUN dotnet tool install --global dotnet-ef && \
    dotnet tool install --global dotnet-aspnet-codegenerator 

# Nodejs 26
RUN curl -sL https://deb.nodesource.com/setup_26.x | bash \
    && apt-get install nodejs -yq 
# PNPM
ENV SHELL=/bin/bash
RUN curl -fsSL https://get.pnpm.io/install.sh | sh -

# Open Spec

# Open Code
